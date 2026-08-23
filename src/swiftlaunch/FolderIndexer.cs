using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace SwiftLaunch
{
    public class SearchResult
    {
        public string Path { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsRecent { get; set; }
        public double Score { get; set; }
    }

    public class FolderIndexer : IDisposable
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private CancellationTokenSource? _indexCts;
        private readonly object _searchLock = new();
        private bool _disposed;

        // In-memory hot cache (top 500 recent + frequent folders)
        private volatile List<(string Path, string Name, int Frequency)> _hotCache = new();
        private readonly HashSet<string> _recentOpens = new();
        private readonly object _cacheLock = new();

        // FileSystemWatchers, one per fixed drive
        private readonly List<FileSystemWatcher> _watchers = new();

        // Debounce: coalesce rapid filesystem events into one background refresh
        private readonly System.Timers.Timer _watcherDebounce;
        private const int WatcherDebounceMs = 3000;

        // Drives to skip (system/hidden)
        private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "$Recycle.Bin", "System Volume Information", "Windows", "ProgramData",
            "Recovery", "Config.Msi", ".git", "node_modules", "__pycache__",
            "AppData", ".vs", "obj", "bin", "packages", ".nuget"
        };

        private const int MaxDepth = 8;

        // Event raised when the index or hot cache is updated (reindex, rename, record open)
        public event EventHandler? IndexChanged;

        private void OnIndexChanged()
        {
            try { IndexChanged?.Invoke(this, EventArgs.Empty); } catch { }
        }

        public FolderIndexer()
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SwiftLaunch");
            Directory.CreateDirectory(appData);
            _dbPath = Path.Combine(appData, "index.db");
            _connectionString = $"Data Source={_dbPath};Cache=Shared;";
            InitializeDatabase();
            LoadHotCache();

            _watcherDebounce = new System.Timers.Timer(WatcherDebounceMs) { AutoReset = false };
            _watcherDebounce.Elapsed += (_, _) => ForceReindex();
        }

        // Simple file logger for diagnosing watcher / update issues.
        private void DebugLog(string msg)
        {
            try
            {
                var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwiftLaunch");
                Directory.CreateDirectory(appData);
                var f = Path.Combine(appData, "debug.log");
                var line = DateTimeOffset.UtcNow.ToString("o") + " " + msg + "\n";
                File.AppendAllText(f, line);
            }
            catch { }
        }

        private void InitializeDatabase()
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS folders (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL COLLATE NOCASE,
                    path TEXT NOT NULL UNIQUE,
                    depth INTEGER DEFAULT 0,
                    indexed_at INTEGER DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS idx_name ON folders(name COLLATE NOCASE);
                
                CREATE TABLE IF NOT EXISTS recent_opens (
                    path TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    open_count INTEGER DEFAULT 1,
                    last_open INTEGER DEFAULT 0
                );
                
                CREATE TABLE IF NOT EXISTS meta (
                    key TEXT PRIMARY KEY,
                    value TEXT
                );
                """;
            cmd.ExecuteNonQuery();
        }

        private SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var pragma = conn.CreateCommand();
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA cache_size=4000;";
            pragma.ExecuteNonQuery();
            return conn;
        }

        public void StartBackgroundIndex()
        {
            _indexCts = new CancellationTokenSource();
            var token = _indexCts.Token;

            Task.Run(async () =>
            {
                if (!NeedsReindex())
                    await Task.Delay(TimeSpan.FromMinutes(5), token);

                await RunIndexAsync(token);

                // Start watchers after initial index is done
                StartWatchers();
            }, token);
        }

        public void ForceReindex()
        {
            _indexCts?.Cancel();
            _indexCts = new CancellationTokenSource();
            Task.Run(() => RunIndexAsync(_indexCts.Token));
        }

        // Attach one FileSystemWatcher per fixed/removable drive
        private void StartWatchers()
        {
            if (_disposed) return;

            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady &&
                            (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                .Select(d => d.RootDirectory.FullName);

            foreach (var root in drives)
            {
                try
                {
                    var watcher = new FileSystemWatcher(root)
                    {
                        NotifyFilter          = NotifyFilters.DirectoryName,
                        IncludeSubdirectories = true,
                        EnableRaisingEvents   = true
                    };

                    watcher.Created += OnFsEvent;
                    watcher.Deleted += OnFsEvent;
                    watcher.Renamed += OnFsRenamed;
                    watcher.Error   += OnWatcherError;

                    _watchers.Add(watcher);
                }
                catch { /* Drive not accessible — skip */ }
            }
        }

        // Created / Deleted: need full re-index to discover subtree changes
        private void OnFsEvent(object sender, FileSystemEventArgs e)
        {
            DebugLog($"OnFsEvent: {e.ChangeType} {e.FullPath}");

            // Try a fast-path update for single-folder create/delete events so
            // the UI can reflect the change instantly without a full reindex.
            try
            {
                if (e.ChangeType == WatcherChangeTypes.Created)
                {
                    TryApplyCreate(e.FullPath);
                    return;
                }
                if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    TryApplyDelete(e.FullPath);
                    return;
                }
            }
            catch { /* non-fatal, fall back to full reindex */ }

            RestartDebounce();
        }

        // Fast-path: insert a single created folder into the folders table
        private void TryApplyCreate(string path)
        {
            try
            {
                if (!Directory.Exists(path)) return;

                var name = Path.GetFileName(path);
                if (string.IsNullOrEmpty(name)) return;

                long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int depth = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Length - 1;

                using var conn = OpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT OR REPLACE INTO folders(name, path, depth, indexed_at) VALUES(@name,@path,@depth,@ts)";
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@path", path);
                cmd.Parameters.AddWithValue("@depth", depth);
                cmd.Parameters.AddWithValue("@ts", ts);
                var rows = cmd.ExecuteNonQuery();
                DebugLog($"TryApplyCreate: inserted/updated {path} rows={rows}");

                // Reload cache and notify UI
                LoadHotCache();
                OnIndexChanged();

                // schedule full reindex in case more changes happen in the subtree
                RestartDebounce();
            }
            catch (Exception ex) { DebugLog($"TryApplyCreate exception: {ex.Message}"); }
        }

        // Fast-path: remove deleted folder rows from folders and recent_opens
        private void TryApplyDelete(string path)
        {
            try
            {
                using var conn = OpenConnection();
                using var tx = conn.BeginTransaction();

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM folders WHERE path = @p OR path LIKE @prefix ESCAPE '\\'";
                    cmd.Parameters.AddWithValue("@p", path);
                    cmd.Parameters.AddWithValue("@prefix", path.Replace("\\", "\\\\") + "\\%" );
                    var del1 = cmd.ExecuteNonQuery();
                    DebugLog($"TryApplyDelete: folders deleted for {path} rows={del1}");
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM recent_opens WHERE path = @p OR path LIKE @prefix ESCAPE '\\'";
                    cmd.Parameters.AddWithValue("@p", path);
                    cmd.Parameters.AddWithValue("@prefix", path.Replace("\\", "\\\\") + "\\%" );
                    var del2 = cmd.ExecuteNonQuery();
                    DebugLog($"TryApplyDelete: recent_opens deleted for {path} rows={del2}");
                }

                tx.Commit();

                LoadHotCache();
                OnIndexChanged();
                // schedule full reindex in case child entries were missed
                RestartDebounce();
            }
            catch (Exception ex) { DebugLog($"TryApplyDelete exception: {ex.Message}"); }
        }

        // Renamed: handle surgically — update DB rows in-place, reload cache immediately
        private void OnFsRenamed(object sender, RenamedEventArgs e)
        {
            if (_disposed) return;
            DebugLog($"OnFsRenamed: {e.OldFullPath} -> {e.FullPath}");
            Task.Run(() =>
            {
                try
                {
                    ApplyRename(e.OldFullPath, e.FullPath);
                    DebugLog($"ApplyRename completed for {e.OldFullPath} -> {e.FullPath}");
                }
                catch (Exception ex)
                {
                    DebugLog($"ApplyRename task exception: {ex.Message}");
                }
            });
            // Also schedule a debounced full reindex as a safety net in case
            // the surgical update missed something (e.g. locked files, timing).
            RestartDebounce();
        }

        private void ApplyRename(string oldPath, string newPath)
        {
            try
            {
                var newName = Path.GetFileName(newPath);
                if (string.IsNullOrEmpty(newName)) return;

                using var conn = OpenConnection();
                using var tx   = conn.BeginTransaction();

                // ── folders table ──────────────────────────────────────────────────

                // 1. Update the renamed folder itself
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = """
                        UPDATE folders
                        SET name = @newName, path = @newPath
                        WHERE path = @oldPath
                        """;
                    cmd.Parameters.AddWithValue("@newName", newName);
                    cmd.Parameters.AddWithValue("@newPath", newPath);
                    cmd.Parameters.AddWithValue("@oldPath", oldPath);
                    cmd.ExecuteNonQuery();
                }

                // 2. Update every child path under the renamed folder
                using (var cmd = conn.CreateCommand())
                {
                    var esc = LikeEscape(oldPath);
                    cmd.CommandText = """
                        UPDATE folders
                        SET path = @newPath || substr(path, length(@oldPath) + 1)
                        WHERE path LIKE @prefix ESCAPE '\'
                        """;
                    cmd.Parameters.AddWithValue("@newPath", newPath);
                    cmd.Parameters.AddWithValue("@oldPath", oldPath);
                    cmd.Parameters.AddWithValue("@prefix",  esc + @"\%");
                    cmd.ExecuteNonQuery();
                }

                // ── recent_opens table ─────────────────────────────────────────────
                //
                // `path` is the PRIMARY KEY of recent_opens.
                // SQLite does not allow UPDATE SET path= on a PK column — the row stays
                // at the old key value even if the statement appears to succeed.
                //
                // Correct approach: read the old row, INSERT OR REPLACE with the new key
                // (preserving open_count + last_open), then DELETE the old row.
                // This is done inside the same transaction so no data is ever lost.

                // 3a. Rename exact match in recent_opens
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = """
                        INSERT OR REPLACE INTO recent_opens(path, name, open_count, last_open)
                        SELECT @newPath, @newName, open_count, last_open
                        FROM   recent_opens
                        WHERE  path = @oldPath
                        """;
                    cmd.Parameters.AddWithValue("@newPath", newPath);
                    cmd.Parameters.AddWithValue("@newName", newName);
                    cmd.Parameters.AddWithValue("@oldPath", oldPath);
                    cmd.ExecuteNonQuery();
                }
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM recent_opens WHERE path = @oldPath";
                    cmd.Parameters.AddWithValue("@oldPath", oldPath);
                    cmd.ExecuteNonQuery();
                }

                // 3b. Rename child entries in recent_opens
                //     Fetch first (can't mutate PK while iterating), then re-insert + delete.
                var childRows = new List<(string OldP, string NewP, string NewN, int Count, long LastOpen)>();
                using (var cmd = conn.CreateCommand())
                {
                    var esc = LikeEscape(oldPath);
                    cmd.CommandText = """
                        SELECT path, open_count, last_open FROM recent_opens
                        WHERE  path LIKE @prefix ESCAPE '\'
                        """;
                    cmd.Parameters.AddWithValue("@prefix", esc + @"\%");
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        var op = rdr.GetString(0);
                        var np = newPath + op[oldPath.Length..];  // swap the prefix
                        childRows.Add((op, np, Path.GetFileName(np), rdr.GetInt32(1), rdr.GetInt64(2)));
                    }
                }
                foreach (var (op, np, nm, cnt, lo) in childRows)
                {
                    using var ins = conn.CreateCommand();
                    ins.CommandText = """
                        INSERT OR REPLACE INTO recent_opens(path, name, open_count, last_open)
                        VALUES(@np, @nm, @cnt, @lo)
                        """;
                    ins.Parameters.AddWithValue("@np",  np);
                    ins.Parameters.AddWithValue("@nm",  nm);
                    ins.Parameters.AddWithValue("@cnt", cnt);
                    ins.Parameters.AddWithValue("@lo",  lo);
                    ins.ExecuteNonQuery();

                    using var del = conn.CreateCommand();
                    del.CommandText = "DELETE FROM recent_opens WHERE path = @op";
                    del.Parameters.AddWithValue("@op", op);
                    del.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch (Exception ex) { DebugLog($"ApplyRename exception: {ex.Message}"); }

            // Reload hot cache — new name is now visible to Search() immediately
            LoadHotCache();
            DebugLog($"ApplyRename: reloaded hot cache and notifying IndexChanged");
            OnIndexChanged();
        }

        // Escape LIKE special characters in a path string
        private static string LikeEscape(string path) =>
            path.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

        private void OnWatcherError(object sender, ErrorEventArgs e) => RestartDebounce();

        private void RestartDebounce()
        {
            if (_disposed) return;
            _watcherDebounce.Stop();
            _watcherDebounce.Start();
        }

        private bool NeedsReindex()
        {
            try
            {
                using var conn = OpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT value FROM meta WHERE key='last_index'";
                var val = cmd.ExecuteScalar()?.ToString();
                if (val == null) return true;
                var lastIndex = DateTimeOffset.FromUnixTimeSeconds(long.Parse(val));
                return (DateTimeOffset.UtcNow - lastIndex).TotalHours > 12;
            }
            catch { return true; }
        }

        private async Task RunIndexAsync(CancellationToken token)
        {
            // BUG FIX: every row written during this run is stamped with runTs
            // (see FlushBatch). Once the scan finishes without being cancelled,
            // anything still holding an OLDER indexed_at is a folder that used
            // to exist but wasn't seen this time around — i.e. it was deleted
            // or renamed away — so it's swept from the index below. This is
            // what makes deletes disappear from suggestions, not just creates.
            var runTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                .Select(d => d.RootDirectory.FullName)
                .ToList();

            var batch = new List<(string Name, string Path, int Depth)>(500);

            foreach (var root in drives)
            {
                if (token.IsCancellationRequested) return;
                await ScanDirectoryAsync(root, 0, batch, token, runTs);
            }

            if (token.IsCancellationRequested) return;
            if (batch.Count > 0) FlushBatch(batch, runTs);

            using (var conn = OpenConnection())
            {
                // Sweep stale rows (folders no longer on disk) — only safe to do
                // once the full scan has completed without cancellation, so a
                // partial/aborted scan never wipes out folders it simply hadn't
                // reached yet.
                using (var cleanup = conn.CreateCommand())
                {
                    cleanup.CommandText = "DELETE FROM folders WHERE indexed_at < @runTs";
                    cleanup.Parameters.AddWithValue("@runTs", runTs);
                    cleanup.ExecuteNonQuery();
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT OR REPLACE INTO meta(key, value) VALUES('last_index', @ts)
                    """;
                cmd.Parameters.AddWithValue("@ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                cmd.ExecuteNonQuery();
            }

            LoadHotCache();
        }

        private async Task ScanDirectoryAsync(
            string path, int depth,
            List<(string Name, string Path, int Depth)> batch,
            CancellationToken token,
            long runTs)
        {
            if (depth > MaxDepth || token.IsCancellationRequested) return;

            string[] subdirs;
            try { subdirs = Directory.GetDirectories(path); }
            catch { return; }

            foreach (var dir in subdirs)
            {
                if (token.IsCancellationRequested) return;

                var name = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(name) || name.StartsWith('.') || SkipNames.Contains(name))
                    continue;

                batch.Add((name, dir, depth));

                if (batch.Count >= 500)
                {
                    FlushBatch(batch, runTs);
                    batch.Clear();
                    await Task.Delay(10, token);
                }

                await ScanDirectoryAsync(dir, depth + 1, batch, token, runTs);
            }
        }

        private void FlushBatch(List<(string Name, string Path, int Depth)> batch, long runTs)
        {
            try
            {
                using var conn = OpenConnection();
                using var tx   = conn.BeginTransaction();
                using var cmd  = conn.CreateCommand();
                // BUG FIX: was "INSERT OR IGNORE", which meant a folder already
                // in the table kept its OLD indexed_at forever, so it looked
                // "stale" and got swept even though it still exists — and worse,
                // a truly deleted folder's row was never touched/removed at all.
                // INSERT OR REPLACE refreshes indexed_at on every row we still
                // see on disk, which is what lets the sweep in RunIndexAsync
                // correctly identify (and only identify) folders that are gone.
                cmd.CommandText = """
                    INSERT OR REPLACE INTO folders(name, path, depth, indexed_at)
                    VALUES(@name, @path, @depth, @ts)
                    """;
                var pName  = cmd.Parameters.Add("@name",  SqliteType.Text);
                var pPath  = cmd.Parameters.Add("@path",  SqliteType.Text);
                var pDepth = cmd.Parameters.Add("@depth", SqliteType.Integer);
                var pTs    = cmd.Parameters.Add("@ts",    SqliteType.Integer);
                pTs.Value  = runTs;

                foreach (var (name, path, depth) in batch)
                {
                    pName.Value  = name;
                    pPath.Value  = path;
                    pDepth.Value = depth;
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch { }
            // Notify UI consumers that the hot cache may have changed.
            OnIndexChanged();
        }

        // ── BUG FIX: LoadHotCache now filters out entries whose paths no longer exist ──
        private void LoadHotCache()
        {
            try
            {
                // Read all recent entries from DB
                var raw = new List<(string Path, string Name, int Frequency)>(220);

                using var conn = OpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT path, name, open_count FROM recent_opens
                    ORDER BY last_open DESC LIMIT 200
                    """;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        raw.Add((reader.GetString(0), reader.GetString(1), reader.GetInt32(2) + 100));
                }

                // Safety filter: drop any entry whose folder no longer exists on disk.
                // This handles deletes, moves, and any rename that wasn't caught live.
                var list = raw
                    .Where(r => Directory.Exists(r.Path))
                    .ToList();

                lock (_cacheLock)
                    _hotCache = list;
            }
            catch { }
        }

        // ── FEATURE: default suggestions shown as soon as the launcher opens,
        //    before any typing — top N folders blending frequency (open_count)
        //    and recency (hot cache is loaded most-recently-opened first, so a
        //    stable sort on frequency keeps recently-opened folders ahead of
        //    older ones with the same open_count). Refreshes automatically
        //    because RecordOpen() reloads the hot cache after every open.
        public List<SearchResult> GetDefaultSuggestions(int maxResults = 5)
        {
            lock (_cacheLock)
            {
                return _hotCache
                    .OrderByDescending(h => h.Frequency)
                    .Take(maxResults)
                    .Select(h => new SearchResult
                    {
                        Path     = h.Path,
                        Name     = h.Name,
                        Score    = h.Frequency,
                        IsRecent = true
                    })
                    .ToList();
            }
        }

        public List<SearchResult> Search(string query, int maxResults = 8)
        {
            if (string.IsNullOrWhiteSpace(query)) return new();

            query = query.Trim();
            var results = new List<SearchResult>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            lock (_cacheLock)
            {
                foreach (var (path, name, freq) in _hotCache)
                {
                    double score = ScoreMatch(name, query) + (freq * 0.01);
                    if (score > 0)
                    {
                        results.Add(new SearchResult
                        {
                            Path     = path,
                            Name     = name,
                            Score    = score + 50,
                            IsRecent = true
                        });
                        seen.Add(path);
                    }
                }
            }

            try
            {
                using var conn = OpenConnection();
                using var cmd  = conn.CreateCommand();
                cmd.CommandText = """
                    SELECT name, path FROM folders
                    WHERE name LIKE @q COLLATE NOCASE
                    ORDER BY
                        CASE WHEN name = @exact THEN 0
                             WHEN name LIKE @start THEN 1
                             ELSE 2 END,
                        depth ASC
                    LIMIT 50
                    """;
                cmd.Parameters.AddWithValue("@q",     $"%{query}%");
                cmd.Parameters.AddWithValue("@exact", query);
                cmd.Parameters.AddWithValue("@start", $"{query}%");

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var path = reader.GetString(1);
                    if (seen.Contains(path)) continue;
                    var name = reader.GetString(0);
                    double score = ScoreMatch(name, query);
                    if (score > 0)
                    {
                        results.Add(new SearchResult { Path = path, Name = name, Score = score });
                        seen.Add(path);
                    }
                }
            }
            catch { }

            return results
                .OrderByDescending(r => r.Score)
                .Take(maxResults)
                .ToList();
        }

        private static double ScoreMatch(string name, string query)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase)) return 100;
            if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 80;
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase)) return 50;
            if (IsAcronymMatch(name, query)) return 40;
            return 0;
        }

        private static bool IsAcronymMatch(string name, string query)
        {
            if (query.Length < 2 || query.Length > 6) return false;
            var initials = new string(name.Where(c => char.IsUpper(c) || c == '-' || c == '_')
                                         .Take(8).ToArray()).ToLowerInvariant();
            return initials.StartsWith(query.ToLowerInvariant());
        }

        public void RecordOpen(string path)
        {
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name)) name = path;

            Task.Run(() =>
            {
                try
                {
                    using var conn = OpenConnection();
                    using var cmd  = conn.CreateCommand();
                    cmd.CommandText = """
                        INSERT INTO recent_opens(path, name, open_count, last_open)
                        VALUES(@path, @name, 1, @ts)
                        ON CONFLICT(path) DO UPDATE SET
                            open_count = open_count + 1,
                            last_open  = @ts
                        """;
                    cmd.Parameters.AddWithValue("@path", path);
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@ts",   DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    cmd.ExecuteNonQuery();
                }
                catch { }
                LoadHotCache();
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _indexCts?.Cancel();
            _indexCts?.Dispose();

            _watcherDebounce.Stop();
            _watcherDebounce.Dispose();
            foreach (var w in _watchers)
            {
                w.EnableRaisingEvents = false;
                w.Dispose();
            }
            _watchers.Clear();
        }
    }
}
