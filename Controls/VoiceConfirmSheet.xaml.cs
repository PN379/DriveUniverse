using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using DriveUniverse.Services;
using DriveUniverse.Voice;

namespace DriveUniverse.Controls
{
    /// <summary>
    /// Fades in under the mascot whenever a voice command is recognised, echoing
    /// what was heard and the action it maps to, with a confirm/cancel row for
    /// destructive actions (eject/power-cycle). Accompanied by a confirm sound.
    /// </summary>
    public partial class VoiceConfirmSheet : UserControl
    {
        private VoiceCommand _pending;

        // set by the window so confirmations route to the real actions
        public Func<VoiceCommand, bool> ConfirmHandler;

        public VoiceConfirmSheet()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;
        }

        public void Show(VoiceCommand c)
        {
            _pending = c;
            Heard.Text = "“" + c.RawText + "”";
            Action.Text = ActionText(c);
            bool destructive = c.Intent == VoiceIntent.Eject || c.Intent == VoiceIntent.Disconnect;
            ConfirmRow.Visibility = destructive ? Visibility.Visible : Visibility.Collapsed;

            Visibility = Visibility.Visible;
            IsHitTestVisible = destructive;
            this.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3)));
            SoundService.Instance.Play(SoundService.Clip.Confirm);

            // auto-fade for non-destructive commands
            if (!destructive)
            {
                var outA = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5)) { BeginTime = TimeSpan.FromSeconds(2.2) };
                outA.Completed += (s, e) => { Visibility = Visibility.Collapsed; IsHitTestVisible = false; };
                this.BeginAnimation(OpacityProperty, outA);
                // immediately route non-destructive commands (open app, status, etc.)
                ConfirmHandler?.Invoke(c);
            }
        }

        private void Yes_Click(object s, RoutedEventArgs e)
        {
            ConfirmHandler?.Invoke(_pending);
            Hide();
        }

        private void No_Click(object s, RoutedEventArgs e) => Hide();

        public void Hide()
        {
            var outA = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.25));
            outA.Completed += (s, e) => { Visibility = Visibility.Collapsed; IsHitTestVisible = false; };
            this.BeginAnimation(OpacityProperty, outA);
        }

        private string ActionText(VoiceCommand c)
        {
            switch (c.Intent)
            {
                case VoiceIntent.Eject: return "→ Eject the drive?";
                case VoiceIntent.Connect: return "→ Connect / power-cycle the drive?";
                case VoiceIntent.Disconnect: return "→ Disconnect the USB device?";
                case VoiceIntent.OpenApp: return "→ Open " + (c.Argument ?? "app");
                case VoiceIntent.Status: return "→ Reporting drive status";
                case VoiceIntent.Help: return "→ Showing available commands";
                case VoiceIntent.Sleep: return "→ Going to standby";
                case VoiceIntent.Wake: return "→ Hello!";
                default: return "";
            }
        }
    }
}
