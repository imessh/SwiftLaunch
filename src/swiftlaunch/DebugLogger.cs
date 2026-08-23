using System;
using System.IO;

namespace SwiftLaunch
{
    internal static class DebugLogger
    {
        public static void Log(string msg)
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
    }
}
