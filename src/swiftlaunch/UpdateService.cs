using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;

namespace SwiftLaunch
{
    /// <summary>
    /// Holds information about an available GitHub release.
    /// </summary>
    public sealed class UpdateInfo
    {
        /// <summary>The latest version tag from GitHub (e.g. "v1.2.0").</summary>
        public string LatestVersion { get; init; } = "";

        /// <summary>The release notes body from GitHub.</summary>
        public string ReleaseNotes { get; init; } = "";

        /// <summary>Direct URL to the GitHub Releases page for this tag.</summary>
        public string ReleasePageUrl { get; init; } = "";
    }

    /// <summary>
    /// Checks the GitHub Releases API and compares the latest tag against
    /// the running assembly version. No auto-install; detection only.
    /// </summary>
    public sealed class UpdateService
    {
        // ── Configuration ──────────────────────────────────────────────────────
        // Replace these two values with your actual GitHub owner and repository name.
        private const string GitHubOwner = "imessh";
        private const string GitHubRepo  = "SwiftLaunch";

        private static readonly string ApiUrl =
            $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

        private static readonly string ReleasesUrl =
            $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";

        // GitHub API requires a User-Agent header; use the app name + version.
        private static readonly HttpClient Http = new()
        {
            DefaultRequestHeaders =
            {
                { "User-Agent", $"SwiftLaunch/{GetCurrentVersion()}" },
                { "Accept",     "application/vnd.github+json"        }
            },
            Timeout = TimeSpan.FromSeconds(10)
        };

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the running assembly's version string (e.g. "1.0.0").
        /// Reads from the assembly version set in the .csproj &lt;Version&gt; property.
        /// </summary>
        public static string GetCurrentVersion()
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
        }

        /// <summary>
        /// Queries the GitHub Releases API.
        /// Returns an <see cref="UpdateInfo"/> if a newer version is available,
        /// or <c>null</c> if the app is up to date or the check fails.
        /// </summary>
        public static async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                var release = await Http.GetFromJsonAsync<GitHubRelease>(ApiUrl)
                              .ConfigureAwait(false);

                if (release is null || string.IsNullOrWhiteSpace(release.TagName))
                    return null;

                // Strip leading 'v' so "v1.2.0" → "1.2.0" for comparison
                var latestTag     = release.TagName.TrimStart('v', 'V');
                var currentStr    = GetCurrentVersion();

                if (!Version.TryParse(latestTag,  out var latestVersion))  return null;
                if (!Version.TryParse(currentStr, out var currentVersion)) return null;

                // Only notify when the remote version is strictly newer
                if (latestVersion <= currentVersion) return null;

                return new UpdateInfo
                {
                    LatestVersion  = release.TagName,               // keep original tag for display
                    ReleaseNotes   = release.Body ?? "",
                    ReleasePageUrl = release.HtmlUrl ?? ReleasesUrl
                };
            }
            catch
            {
                // Network errors, parse errors, timeouts — fail silently.
                // Update check is a best-effort background operation.
                return null;
            }
        }

        /// <summary>
        /// Opens the GitHub Releases page in the user's default browser.
        /// Called when the user clicks [Update Now].
        /// </summary>
        public static void OpenReleasePage(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                url = ReleasesUrl;

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName        = url,
                    UseShellExecute = true   // lets Windows open it in the default browser
                });
            }
            catch { /* Non-fatal — browser could not be launched */ }
        }

        // ── Private deserialization model ──────────────────────────────────────

        /// <summary>Minimal mapping of the GitHub releases/latest JSON response.</summary>
        private sealed class GitHubRelease
        {
            [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
            public string? TagName { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("body")]
            public string? Body { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("html_url")]
            public string? HtmlUrl { get; set; }
        }
    }
}
