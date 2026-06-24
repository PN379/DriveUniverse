using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DriveUniverse.Services;
using DriveUniverse.Voice;

namespace DriveUniverse.Views
{
    public partial class VoiceView : UserControl
    {
        public VoiceView()
        {
            InitializeComponent();
            Loaded += (s, e) => LateInit();
        }

        private void LateInit()
        {
            // Populate language selector
            if (Host.Voice != null)
            {
                var cultures = Host.Voice.GetAvailableCultures();
                foreach (var c in cultures)
                    CmbLanguage.Items.Add(c.name + " [" + c.culture + "]");
                if (CmbLanguage.Items.Count > 0)
                    CmbLanguage.SelectedIndex = 0;
                CmbLanguage.SelectionChanged += Language_Changed;
            }

            UpdateStatus();
            ChkEnable.IsChecked = LauncherService.Instance.VoiceEnabled;
            ChkAlways.IsChecked = LauncherService.Instance.AlwaysListening;
            ChkWakeWord.IsChecked = LauncherService.Instance.WakeWordEnabled;
            ChkWidgetQuick.IsChecked = LauncherService.Instance.ShowWidgetQuickActions;

            // Attach handlers AFTER init
            ChkEnable.Checked += Enable_Changed;
            ChkEnable.Unchecked += Enable_Changed;
            ChkAlways.Checked += Always_Changed;
            ChkAlways.Unchecked += Always_Changed;
            ChkWakeWord.Checked += WakeWord_Changed;
            ChkWakeWord.Unchecked += WakeWord_Changed;
            ChkWidgetQuick.Checked += WidgetQuick_Changed;
            ChkWidgetQuick.Unchecked += WidgetQuick_Changed;
            BtnPTT.PreviewMouseDown += PTT_Down;
            BtnPTT.PreviewMouseUp += PTT_Up;

            RefreshEnabled();

            // Hook wake word events for UI feedback
            if (Host.Voice != null)
            {
                Host.Voice.WakeWordHeard += () => Dispatcher.Invoke(() =>
                {
                    Ui.MascotListening(true);
                    Ui.Toast("Orbit is listening...");
                });
                Host.Voice.CommandWindowExpired += () => Dispatcher.Invoke(() =>
                {
                    if (LauncherService.Instance.WakeWordEnabled)
                        Ui.MascotListening(false);
                });
            }

            if (!Host.VoiceReady && Host.Voice?.DiagnosticsLog != null)
                ShowDiagnostics();
        }

        private void UpdateStatus()
        {
            if (!Host.VoiceReady)
            {
                string reason = Host.Voice?.UnavailableReason;
                Status.Text = !string.IsNullOrEmpty(reason) ? reason : "Voice unavailable.";
                ChkEnable.IsEnabled = false;
                BtnPTT.IsEnabled = false;
            }
            else
            {
                Status.Text = "Microphone ready. Enable voice and say 'Orbit' to start.";
            }
        }

        private void RefreshEnabled()
        {
            bool on = ChkEnable.IsChecked == true && Host.VoiceReady;
            ChkAlways.IsEnabled = on;
            ChkWakeWord.IsEnabled = on;
            BtnPTT.IsEnabled = on;
        }

        private void Language_Changed(object s, SelectionChangedEventArgs e)
        {
            if (CmbLanguage.SelectedIndex < 0 || Host.Voice == null) return;
            string selected = CmbLanguage.SelectedItem as string;
            if (string.IsNullOrEmpty(selected)) return;
            int start = selected.IndexOf('[');
            int end = selected.IndexOf(']');
            if (start >= 0 && end > start)
            {
                string culture = selected.Substring(start + 1, end - start - 1);
                Host.Voice.SelectedCulture = culture;
                Host.Voice.Initialize();
                UpdateStatus();
                // Re-start listening if it was on
                if (ChkEnable.IsChecked == true && ChkAlways.IsChecked == true)
                    Host.Voice.StartAlwaysListening();
                Ui.Toast("Language: " + culture);
            }
        }

        private void Diag_Click(object s, RoutedEventArgs e)
        {
            if (Host.Voice != null) Host.Voice.Initialize();
            ShowDiagnostics();
            UpdateStatus();
            RefreshEnabled();
        }

        private void ShowDiagnostics()
        {
            DiagPanel.Visibility = Visibility.Visible;
            DiagOutput.Text = Host.Voice?.DiagnosticsLog ?? "No diagnostics.";
        }

        private void Enable_Changed(object s, RoutedEventArgs e)
        {
            bool on = ChkEnable.IsChecked == true;
            LauncherService.Instance.VoiceEnabled = on;
            LauncherService.Instance.Save();
            RefreshEnabled();
            if (on && Host.VoiceReady)
            {
                Host.Voice.CommandRecognized += OnCommand;
                if (ChkAlways.IsChecked == true)
                    Host.Voice.StartAlwaysListening();
                Ui.Toast("Voice enabled - say 'Orbit'");
                Ui.Sound(SoundService.Clip.Listen);
            }
            else
            {
                Host.Voice?.StopAlwaysListening();
                Ui.MascotListening(false);
            }
        }

        private void Always_Changed(object s, RoutedEventArgs e)
        {
            bool on = ChkAlways.IsChecked == true;
            LauncherService.Instance.AlwaysListening = on;
            LauncherService.Instance.Save();
            if (on && Host.Voice != null) Host.Voice.StartAlwaysListening();
            else Host.Voice?.StopAlwaysListening();
            Ui.Toast(on ? "Always listening" : "Push-to-talk only");
        }

        private void WakeWord_Changed(object s, RoutedEventArgs e)
        {
            bool on = ChkWakeWord.IsChecked == true;
            LauncherService.Instance.WakeWordEnabled = on;
            LauncherService.Instance.Save();
            Host.Voice?.UpdateWakeMode();
            Ui.Toast(on ? "Wake word ON - say 'Orbit' first" : "Wake word OFF - always active");
        }

        private void WidgetQuick_Changed(object s, RoutedEventArgs e)
        {
            bool on = ChkWidgetQuick.IsChecked == true;
            LauncherService.Instance.ShowWidgetQuickActions = on;
            LauncherService.Instance.Save();
        }

        private void PTT_Down(object s, MouseButtonEventArgs e)
        {
            if (ChkEnable.IsChecked != true || Host.Voice == null) return;
            Host.Voice.BeginPushToTalk();
            Ui.MascotListening(true);
            Ui.Sound(SoundService.Clip.Listen);
        }

        private void PTT_Up(object s, MouseButtonEventArgs e)
        {
            if (Host.Voice == null) return;
            Host.Voice.EndPushToTalk();
            if (ChkAlways.IsChecked != true && LauncherService.Instance.WakeWordEnabled)
                Ui.MascotListening(false);
        }

        private void OnCommand(VoiceCommand c)
        {
            Dispatcher.Invoke(() => { Ui.Toast("Heard: " + c.RawText); Ui.Sound(SoundService.Clip.Confirm); });
        }
    }
}
