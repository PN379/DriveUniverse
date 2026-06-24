using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DriveUniverse.Services;

namespace DriveUniverse
{
    public partial class App : Application
    {
        // Single-instance guard so the tray launch doesn't spawn duplicates.
        private static readonly System.Threading.Mutex Mutex =
            new System.Threading.Mutex(true, "Global\\DriveUniverse_SingleInstance");

        // *** Version stamp — logged on startup so you can verify you have the latest. ***
        public const string BuildVersion = "BUILD-2026-06-20-beta-1.0.0";

        protected override void OnStartup(StartupEventArgs e)
        {
            // ---- global exception traps: log instead of dying silently ----
            DispatcherUnhandledException += OnDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTask;

            LogService.Log("==== DriveUniverse starting (" + BuildVersion + ") ====");
            LogService.Log("Log file: " + LogService.LogPath);
            LogService.Log("Runtime: " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);

            bool safe = false;
            foreach (string a in e.Args)
                if (a.Equals("/safe", StringComparison.OrdinalIgnoreCase)) safe = true;

            LogService.Log("Safe mode: " + safe);

            try
            {
                if (!Mutex.WaitOne(TimeSpan.Zero, true))
                {
                    LogService.Log("Another instance is already running — exiting.");
                    MessageBox.Show("DriveUniverse is already running (check the system tray).",
                        "DriveUniverse", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown(0);
                    return;
                }
                LogService.Log("Single-instance lock acquired.");

                base.OnStartup(e);

                // CRITICAL: initialise services BEFORE creating the window, so that
                // when WPF constructs the view tree (inside the MainWindow ctor via
                // InitializeComponent), all Host.* singletons are already live.
                LogService.Log("Initialising services (Host.Init)...");
                Host.Init(safe);
                LogService.Log("Services ready.");

                LogService.Log("Creating main window...");
                var window = new MainWindow();
                LogService.Log("Main window created.");

                window.Boot(safe);
                MainWindow = window;

                // ALWAYS show the window on launch so the user sees something.
                LogService.Log("Showing window.");
                window.Show();
                LogService.Log("Startup complete. Window visible.");
            }
            catch (Exception ex)
            {
                LogService.Log("FATAL on startup: " + ex);
                MessageBox.Show(
                    "DriveUniverse failed to start.\n\n" + ex.Message +
                    "\n\nLog written to:\n" + LogService.LogPath +
                    "\n\nRe-run with /safe to skip optional features.",
                    "DriveUniverse — startup error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                LogService.OpenLog();
                Shutdown(1);
            }
        }

        private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogService.Log("UI thread exception: " + e.Exception);
            e.Handled = true;   // keep alive
        }

        private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            LogService.Log("AppDomain unhandled: " + (e.ExceptionObject ?? "(null)"));
        }

        private void OnUnobservedTask(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogService.Log("Unobserved task: " + e.Exception);
            e.SetObserved();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LogService.Log("Exiting (code " + e.ApplicationExitCode + ").");
            try { LauncherService.Instance.Save(); } catch { }
            try { Mutex.ReleaseMutex(); } catch { }
            base.OnExit(e);
        }
    }
}
