using System;
using Microsoft.Win32;
using System.Diagnostics;

namespace SwiftLaunch
{
    public static class StartupManager
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "SwiftLaunch";

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
                // Use AppContext.BaseDirectory for single-file publish compatibility
                var exe = Process.GetCurrentProcess().MainModule?.FileName
                          ?? System.IO.Path.Combine(AppContext.BaseDirectory, "SwiftLaunch.exe");
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                key?.SetValue(AppName, $"\"{exe}\"");
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
    }
}
