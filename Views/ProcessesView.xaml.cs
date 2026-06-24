using System.Windows;
using System.Windows.Controls;
using DriveUniverse.Services;

namespace DriveUniverse.Views
{
    /// <summary>
    /// Live view of every process/service holding the selected drive open (via the
    /// Restart Manager). Recurring offenders are flagged; you can end them or
    /// whitelist them so automation rules may auto-quit them.
    /// </summary>
    public partial class ProcessesView : UserControl
    {
        public ProcessesView()
        {
            InitializeComponent();
            // Host services aren't ready yet during XAML construction (MainWindow
            // constructs all views before calling Boot/Host.Init). Defer to Loaded.
            Loaded += (s, e) => LateInit();
        }

        private void LateInit()
        {
            if (Host.Monitor == null) return;
            List.ItemsSource = Host.Monitor.Current;
            Host.Monitor.ScanCompleted += UpdateEmpty;
            UpdateDriveLabel();
            UpdateEmpty();
        }

        public void UpdateDriveLabel()
        {
            if (Host.Monitor == null) return;
            DriveLbl.Text = Host.Monitor.DriveLetter.HasValue
                ? ("Drive: " + Host.Monitor.DriveLetter.Value + ":")
                : "Drive: —  (pick a drive in Drive Control)";
        }

        private void UpdateEmpty()
        {
            if (Host.Monitor == null) return;
            Dispatcher.Invoke(() =>
                Empty.Visibility = Host.Monitor.Current.Count == 0 ? Visibility.Visible : Visibility.Collapsed);
        }

        private void Scan_Click(object s, RoutedEventArgs e)
        {
            Ui.Sound(SoundService.Clip.Click);
            if (Host.Monitor == null) return;
            Host.Monitor.ScanOnce();
            UpdateDriveLabel();
        }

        private void Terminate_Click(object s, RoutedEventArgs e)
        {
            if (Host.Monitor == null) return;
            if (((FrameworkElement)s).Tag is DriveProcess p && Host.Monitor.Terminate(p))
            {
                Ui.Toast("Ended " + p.AppName);
                Ui.Sound(SoundService.Clip.Click);
                Host.Monitor.ScanOnce();
            }
        }

        private void Whitelist_Click(object s, RoutedEventArgs e)
        {
            if (Host.Monitor == null) return;
            if (((FrameworkElement)s).Tag is DriveProcess p)
            {
                Host.Monitor.MarkKnown(p.ExePath ?? p.AppName);
                Ui.Toast("Whitelisted " + p.AppName + " — rules may auto-quit it.");
            }
        }

        private void AutoQuit_Click(object s, RoutedEventArgs e)
        {
            if (Host.Monitor == null) return;
            int n = Host.Monitor.TerminateRecurring();
            Ui.Toast(n == 0 ? "No recurring processes." : "Ended " + n + " recurring process(es).");
            Ui.Sound(SoundService.Clip.Click);
            Host.Monitor.ScanOnce();
        }
    }
}
