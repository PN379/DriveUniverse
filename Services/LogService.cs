using System;
using System.IO;

namespace DriveUniverse.Services
{
    /// <summary>
    /// Tiny file logger. Writes to %AppData%\DriveUniverse\log.txt so that if the
    /// app ever fails to start we have a timestamped trace of exactly where.
    /// </summary>
    public static class LogService
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DriveUniverse");
        private static readonly string LogFile = Path.Combine(LogDir, "log.txt");
        private static readonly object _lock = new object();

        public static void Log(string msg)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(LogDir);
                    File.AppendAllText(LogFile, "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + msg + Environment.NewLine);
                }
            }
            catch { /* never let logging crash the app */ }
        }

        public static string LogPath => LogFile;

        public static void OpenLog()
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                if (!File.Exists(LogFile)) File.WriteAllText(LogFile, "(empty log)");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LogFile) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
