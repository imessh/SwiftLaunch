using System;
using Microsoft.Win32;
using System.Diagnostics;

namespace SwiftLaunch
{
    public static class StartupManager
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "SwiftLaunch";

        private static string GetExpectedCommand()
        {
            // Use AppContext.BaseDirectory for single-file publish compatibility
            var exe = Process.GetCurrentProcess().MainModule?.FileName
                      ?? System.IO.Path.Combine(AppContext.BaseDirectory, "SwiftLaunch.exe");
            return $"\"{exe}\"";
        }

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch { return false; }
        }

        public static void Enable()
        {
            try
            {
                // CreateSubKey opens the key for read/write, creating it if it
                // somehow doesn't exist yet (OpenSubKey would just return null).
                using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
                key?.SetValue(AppName, GetExpectedCommand());
            }
            catch { }
        }

        public static void Disable()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                key?.DeleteValue(AppName, false);
            }
            catch { }
        }

        // ── BUG FIX: repair a stale Run-key entry ──────────────────────────
        // If the app was rebuilt/republished or moved to a new folder after
        // it first registered for startup, the registry still points at the
        // old (now missing) exe path, so Windows silently fails to launch it
        // at boot. Call this on every startup (only when already enabled) to
        // keep the registered path in sync with where the exe actually is.
        public static void RepairIfStale()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                if (key == null) return;

                var expected = GetExpectedCommand();
                var current  = key.GetValue(AppName) as string;

                if (current != null &&
                    !string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(AppName, expected);
                }
            }
            catch { }
        }
    }
}
