using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using Brush    = System.Windows.Media.Brush;
using WinForms = System.Windows.Forms;

namespace SwiftLaunch
{
    public partial class LauncherWindow : Window
    {
        private readonly FolderIndexer _indexer;
        private CancellationTokenSource? _searchCts;
        private readonly System.Windows.Threading.DispatcherTimer _debounceTimer;

        // Badge brushes
        private static readonly WpfBrush FolderBadgeBrush = new(WpfColor.FromRgb(63,  63,  90));
        private static readonly WpfBrush VSCodeBadgeBrush  = new(WpfColor.FromRgb(14,  84,  120));
        private static readonly WpfBrush RecentBadgeBrush  = new(WpfColor.FromRgb(80,  50,  100));
        private static readonly WpfBrush CreateBadgeBrush  = new(WpfColor.FromRgb(20,  100, 60));

        // Stored update info so the [Update Now] button can open the correct URL
        private UpdateInfo? _pendingUpdate;

        public LauncherWindow(FolderIndexer indexer)
        {
            _indexer = indexer;
            InitializeComponent();

            _debounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80)
            };
            _debounceTimer.Tick += DebounceTimer_Tick;

            SearchBox.TextChanged    += SearchBox_TextChanged;
            SearchBox.PreviewKeyDown += SearchBox_KeyDown;
            SuggestionList.SelectionChanged += SuggestionList_SelectionChanged;

            CenterOnScreen();
            Deactivated += (s, e) => Hide();
        }

        public void ShowAndActivate()
        {
            SearchBox.Text = string.Empty;
            HideSuggestions();
            UpdateModeIndicator(string.Empty);
            ResetStatus();
            CenterOnScreen();
            Show();
            Activate();
            SearchBox.Focus();
            Keyboard.Focus(SearchBox);
        }

        private void CenterOnScreen()
        {
            var screen = WinForms.Screen.PrimaryScreen?.WorkingArea
                         ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
            Left = (screen.Width - Width) / 2 + screen.Left;
            Top  = screen.Height * 0.28 + screen.Top;
        }

        // ─────────────────────────────────────────────────────────────
        //  COMMAND PARSING  (fully replaced — no n. / c. prefixes)
        // ─────────────────────────────────────────────────────────────
        //
        //  Rules:
        //
        //  1 token          → OPEN in File Explorer
        //                     e.g.  "Downloads"
        //
        //  1 word + lone "v"  (2 tokens total, one is exactly "v")
        //                   → OPEN in VS Code
        //                     e.g.  "Downloads v"   OR   "v Downloads"
        //
        //  2 non-v tokens   → CREATE child inside parent (no "v")
        //                     e.g.  "MyProject Documents"
        //
        //  3 tokens where one is lone "v"
        //                   → CREATE + open in VS Code on success
        //                     e.g.  "MyProject Documents v"
        //                           "v MyProject Documents"
        //
        //  "v" is a STANDALONE flag ONLY when the token is exactly "v"
        //  (case-insensitive). "vfolder", "dev", "childv" are NOT flags.
        // ─────────────────────────────────────────────────────────────

        private static (string mode, string searchTerm, string childName, bool openInCode)
            ParseInput(string raw)
        {
            // Split on whitespace, remove empty entries
            var tokens = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0)
                return ("folder", "", "", false);

            // Identify which token indices are the lone "v" flag
            // A token is the "v" flag if and only if it equals "v" exactly (ignore case).
            // We only strip ONE "v" token (the first found leading or trailing).
            bool leadingV  = string.Equals(tokens[0],    "v", StringComparison.OrdinalIgnoreCase);
            bool trailingV = string.Equals(tokens[^1],   "v", StringComparison.OrdinalIgnoreCase);

            // Strip the standalone "v" flag token (leading takes priority)
            string[] payload;
            bool openInCode = false;

            if (leadingV && tokens.Length >= 2)
            {
                payload     = tokens[1..];
                openInCode  = true;
            }
            else if (trailingV && tokens.Length >= 2)
            {
                payload     = tokens[..^1];
                openInCode  = true;
            }
            else
            {
                // No standalone "v" flag found (or "v" is the only token)
                payload     = tokens;
                openInCode  = false;
            }

            // After stripping the flag, decide mode by payload token count
            switch (payload.Length)
            {
                case 0:
                    // Edge case: user typed exactly "v" alone — treat as folder search for "v"
                    return ("folder", "v", "", false);

                case 1:
                    if (openInCode)
                        // "v foldername"  OR  "foldername v"  → VS Code open
                        return ("vscode", payload[0], "", false);
                    else
                        // "foldername"  → File Explorer open
                        return ("folder", payload[0], "", false);

                default:
                    // 2+ tokens in payload → CREATE mode
                    // payload[0] = child folder name
                    // payload[1..] joined = parent search term
                    string childName   = payload[0];
                    string parentQuery = string.Join(" ", payload[1..]);
                    return ("create", parentQuery, childName, openInCode);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  TEXT CHANGED / DEBOUNCE
        // ─────────────────────────────────────────────────────────────

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var text = SearchBox.Text;
            UpdateModeIndicator(text);
            _debounceTimer.Stop();
            if (string.IsNullOrWhiteSpace(text))
            {
                HideSuggestions();
                ResetStatus();
                return;
            }
            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            PerformSearch(SearchBox.Text);
        }

        // ─────────────────────────────────────────────────────────────
        //  MODE INDICATOR
        // ─────────────────────────────────────────────────────────────

        private void UpdateModeIndicator(string text)
        {
            var (mode, _, _, openInCode) = ParseInput(text);

            switch (mode)
            {
                case "vscode":
                    ModeIcon.Text            = "⌗";
                    ModeIcon.Foreground      = new WpfBrush(WpfColor.FromRgb(14, 165, 233));
                    ModeBadge.Background     = new WpfBrush(WpfColor.FromRgb(14, 84, 120));
                    ModeBadgeText.Text       = "VS CODE";
                    ModeBadgeText.Foreground = new WpfBrush(WpfColor.FromRgb(125, 211, 252));
                    ModeBadge.Visibility     = Visibility.Visible;
                    break;

                case "create":
                    ModeIcon.Text            = "+";
                    ModeIcon.Foreground      = new WpfBrush(WpfColor.FromRgb(52, 211, 153));
                    ModeBadge.Background     = new WpfBrush(WpfColor.FromRgb(20, 100, 60));
                    ModeBadgeText.Text       = openInCode ? "NEW + CODE" : "NEW FOLDER";
                    ModeBadgeText.Foreground = new WpfBrush(WpfColor.FromRgb(110, 231, 183));
                    ModeBadge.Visibility     = Visibility.Visible;
                    break;

                default: // folder
                    ModeIcon.Text        = "⌕";
                    ModeIcon.Foreground  = new WpfBrush(WpfColor.FromRgb(99, 102, 241));
                    ModeBadge.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  SEARCH
        // ─────────────────────────────────────────────────────────────

        private async void PerformSearch(string raw)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            var (mode, searchTerm, childName, openInCode) = ParseInput(raw);

            // Create mode: need both child name and a parent search term to show suggestions
            if (mode == "create")
            {
                if (string.IsNullOrWhiteSpace(childName))
                {
                    HideSuggestions();
                    SetStatus("Type: ChildFolder ParentFolder  (add v to open in VS Code)");
                    return;
                }
                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    HideSuggestions();
                    SetStatus($"Creating \"{childName}\" — type a parent folder name");
                    return;
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(searchTerm)) { HideSuggestions(); return; }
            }

            SetStatus("Searching...");

            try
            {
                var results = await Task.Run(() => _indexer.Search(searchTerm, maxResults: 8), token);
                if (token.IsCancellationRequested) return;

                System.Collections.Generic.List<SuggestionItem> items;

                if (mode == "create")
                {
                    items = results.Select(r => new SuggestionItem
                    {
                        FullPath         = r.Path,
                        DisplayName      = r.Name,
                        SubText          = $"Create \"{childName}\" inside {ShortenPath(r.Path)}",
                        Icon             = "+",
                        BadgeText        = openInCode ? "New+Code" : "New",
                        BadgeBrush       = CreateBadgeBrush,
                        IsCreate         = true,
                        NewFolderName    = childName,
                        CreateOpenInCode = openInCode
                    }).ToList();
                }
                else
                {
                    bool isVSCode = mode == "vscode";
                    items = results.Select(r => new SuggestionItem
                    {
                        FullPath    = r.Path,
                        DisplayName = r.Name,
                        SubText     = ShortenPath(r.Path),
                        Icon        = isVSCode ? "⟨⟩" : "📁",
                        BadgeText   = isVSCode ? "VS Code" : (r.IsRecent ? "Recent" : "Folder"),
                        BadgeBrush  = isVSCode ? VSCodeBadgeBrush : (r.IsRecent ? RecentBadgeBrush : FolderBadgeBrush),
                        IsVSCode    = isVSCode
                    }).ToList();
                }

                if (token.IsCancellationRequested) return;

                SuggestionList.ItemsSource = items;

                if (items.Count > 0)
                {
                    ShowSuggestions();
                    SuggestionList.SelectedIndex = 0;
                    SetStatus(mode == "create"
                        ? $"Select parent folder for \"{childName}\""
                        : $"Found {items.Count} result{(items.Count == 1 ? "" : "s")}");
                }
                else
                {
                    HideSuggestions();
                    SetStatus("No folders found — try a different name");
                }
            }
            catch (OperationCanceledException) { }
        }

        // ─────────────────────────────────────────────────────────────
        //  KEYBOARD
        // ─────────────────────────────────────────────────────────────

        private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Hide();
                    e.Handled = true;
                    break;

                case Key.Enter:
                    ExecuteCurrentSelection();
                    e.Handled = true;
                    break;

                case Key.Down:
                    if (SuggestionList.Visibility == Visibility.Visible)
                    {
                        int next = Math.Min(SuggestionList.SelectedIndex + 1, SuggestionList.Items.Count - 1);
                        SuggestionList.SelectedIndex = next;
                        SuggestionList.ScrollIntoView(SuggestionList.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (SuggestionList.Visibility == Visibility.Visible)
                    {
                        int prev = Math.Max(SuggestionList.SelectedIndex - 1, 0);
                        SuggestionList.SelectedIndex = prev;
                        SuggestionList.ScrollIntoView(SuggestionList.SelectedItem);
                    }
                    e.Handled = true;
                    break;
            }
        }

        private void SuggestionList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (SuggestionList.SelectedItem is SuggestionItem item)
                SetStatus(item.IsCreate
                    ? $"Will create: {Path.Combine(item.FullPath, item.NewFolderName)}"
                    : item.FullPath, important: true);
        }

        private void SuggestionList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteCurrentSelection();
        }

        // ─────────────────────────────────────────────────────────────
        //  EXECUTE
        // ─────────────────────────────────────────────────────────────

        private void ExecuteCurrentSelection()
        {
            SuggestionItem? item = null;

            if (SuggestionList.SelectedItem is SuggestionItem selected)
                item = selected;
            else if (SuggestionList.Items.Count > 0 && SuggestionList.Items[0] is SuggestionItem first)
                item = first;

            if (item == null) { SetStatus("No result selected"); return; }

            if (item.IsCreate)
            {
                ExecuteCreate(item);
                return;
            }

            Hide();
            _indexer.RecordOpen(item.FullPath);

            try
            {
                if (item.IsVSCode)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = "code",
                        Arguments       = $"\"{item.FullPath}\"",
                        UseShellExecute = true
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = "explorer.exe",
                        Arguments       = $"\"{item.FullPath}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not open: {ex.Message}", "SwiftLaunch Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  FOLDER CREATION
        // ─────────────────────────────────────────────────────────────

        private void ExecuteCreate(SuggestionItem item)
        {
            var newPath = Path.Combine(item.FullPath, item.NewFolderName);

            if (Directory.Exists(newPath))
            {
                SetStatus($"Folder already exists: {newPath}", important: true, kind: StatusKind.Error);
                return;
            }

            try
            {
                Directory.CreateDirectory(newPath);
                _indexer.RecordOpen(newPath);
                SetStatus($"✓ Folder created: {newPath}", important: true, kind: StatusKind.Success);

                if (item.CreateOpenInCode)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = "code",
                        Arguments       = $"\"{newPath}\"",
                        UseShellExecute = true
                    });
                }
                // Window stays open so user can read the status message.
            }
            catch (Exception ex)
            {
                SetStatus($"Error creating folder: {ex.Message}", important: true, kind: StatusKind.Error);
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────

        private void ShowSuggestions()
        {
            Divider.Visibility        = Visibility.Visible;
            SuggestionList.Visibility = Visibility.Visible;
        }

        private void HideSuggestions()
        {
            Divider.Visibility         = Visibility.Collapsed;
            SuggestionList.Visibility  = Visibility.Collapsed;
            SuggestionList.ItemsSource = null;
        }

        private void SetStatus(string text, bool important = false, StatusKind kind = StatusKind.Info)
        {
            StatusText.Text = text;
            StatusText.Foreground = kind switch
            {
                StatusKind.Success => System.Windows.Media.Brushes.LightGreen,
                StatusKind.Error   => System.Windows.Media.Brushes.IndianRed,
                _                  => important
                                         ? (Brush)FindResource("TextSecondaryBrush")
                                         : (Brush)FindResource("TextMutedBrush")
            };
        }

        private void ResetStatus() =>
            SetStatus("Type folder name · folder v = VS Code · child parent = create");

        private static string ShortenPath(string path)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                return "~" + path[home.Length..];
            return path.Length > 60 ? "..." + path[^57..] : path;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  UPDATE BANNER
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by App.xaml.cs (on the UI thread) when UpdateService detects a
    /// newer version. Reveals the update banner and stores the URL for the button.
    /// </summary>
    public void ShowUpdateBanner(UpdateInfo info)
    {
        _pendingUpdate = info;
        UpdateBannerText.Text  = $"Version {info.LatestVersion} available";
        UpdateBanner.Visibility = Visibility.Visible;
    }

    private void UpdateNowButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateService.OpenReleasePage(_pendingUpdate?.ReleasePageUrl ?? "");
        // Keep the banner visible so the user can click again if the browser
        // didn't open, but hide the launcher so it's not in the way.
        Hide();
    }

    private void UpdateLaterButton_Click(object sender, RoutedEventArgs e)
    {
        // Dismiss for this session — banner stays hidden until next launch
        UpdateBanner.Visibility = Visibility.Collapsed;
    }

    // ─────────────────────────────────────────────────────────────────
    //  DATA MODEL
    // ─────────────────────────────────────────────────────────────────

    public class SuggestionItem
    {
        public string   FullPath         { get; set; } = "";
        public string   DisplayName      { get; set; } = "";
        public string   SubText          { get; set; } = "";
        public string   Icon             { get; set; } = "📁";
        public string   BadgeText        { get; set; } = "Folder";
        public WpfBrush BadgeBrush       { get; set; } = new(WpfColor.FromRgb(63, 63, 90));

        public bool IsVSCode { get; set; }

        public bool   IsCreate         { get; set; }
        public string NewFolderName    { get; set; } = "";
        public bool   CreateOpenInCode { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────
    //  STATUS KIND
    // ─────────────────────────────────────────────────────────────────

    public enum StatusKind { Info, Success, Error }
}
