using System.Text;

namespace LowCodeApp.Maui.Services
{
    //TestFlight's App Store Connect only shows crash counts, and the native crash logs that do arrive are
    //full of mono_ frames instead of the C# call site. Release builds also have no WebView developer tools.
    //So the app keeps its own plain-text log that a tester can pull off the device with Share log.
    public static class AppLog
    {
        const long MaxBytes = 256 * 1024;

        static readonly object Lock = new();
        static bool _initialized;

        public static string LogFilePath => Path.Combine(FileSystem.AppDataDirectory, "app.log");
        static string BackupFilePath => LogFilePath + ".1";

        public static bool HasLog
        {
            get
            {
                try { return File.Exists(LogFilePath) && new FileInfo(LogFilePath).Length > 0; }
                catch { return false; }
            }
        }

        //Guarded so App startup can call this unconditionally without risking a double subscription if
        //something ever calls CreateMauiApp more than once (e.g. tests).
        public static void Initialize()
        {
            lock (Lock)
            {
                if (_initialized) return;
                _initialized = true;
            }

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Write("AppDomain.UnhandledException", e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Write("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };
        }

        public static void Write(string source, Exception e) => Write($"{source}{Environment.NewLine}{e}");

        //Logging must never be the thing that crashes the app, so every failure here is swallowed rather
        //than surfaced - a lost log entry is much cheaper than turning a diagnostic into a new crash.
        public static void Write(string message)
        {
            try
            {
                lock (Lock)
                {
                    RollIfTooBig();
                    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}{new string('-', 40)}{Environment.NewLine}";
                    File.AppendAllText(LogFilePath, line, Encoding.UTF8);
                }
            }
            catch { }
        }

        public static string ReadTail(int maxChars)
        {
            try
            {
                lock (Lock)
                {
                    if (!File.Exists(LogFilePath)) return string.Empty;
                    var text = File.ReadAllText(LogFilePath, Encoding.UTF8);
                    return text.Length <= maxChars ? text : text[^maxChars..];
                }
            }
            catch { return string.Empty; }
        }

        public static void Clear()
        {
            try
            {
                lock (Lock)
                {
                    if (File.Exists(LogFilePath)) File.Delete(LogFilePath);
                    if (File.Exists(BackupFilePath)) File.Delete(BackupFilePath);
                }
            }
            catch { }
        }

        //Keep one prior generation instead of growing forever or truncating mid-entry - a device log is
        //rarely reopened, so a coarse size cap is enough and simpler than a rolling window.
        static void RollIfTooBig()
        {
            try
            {
                if (!File.Exists(LogFilePath) || new FileInfo(LogFilePath).Length <= MaxBytes) return;
                if (File.Exists(BackupFilePath)) File.Delete(BackupFilePath);
                File.Move(LogFilePath, BackupFilePath);
            }
            catch { }
        }
    }
}
