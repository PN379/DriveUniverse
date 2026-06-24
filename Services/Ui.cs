using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using DriveUniverse.Controls;

namespace DriveUniverse.Services
{
    /// <summary>
    /// Decouples the views from the window: views call Ui.* helpers and the
    /// MainWindow wires them to the real controls + mascot + overlay.
    /// </summary>
    public static class Ui
    {
        public static WarpOverlay Overlay;
        public static MascotView Mascot;
        public static TextBlock ToastBox;
        public static Action<string> Logger;
        public static Action<string, bool> ResultCallback;

        public static void Sound(SoundService.Clip c) => SoundService.Instance.Play(c);
        public static void MascotHappy() => Mascot?.Happy();
        public static void MascotUhOh() => Mascot?.UhOh();
        public static void MascotListening(bool on) => Mascot?.SetListening(on);
        public static void Log(string msg) => Logger?.Invoke(msg);

        public static void Toast(string msg)
        {
            if (ToastBox == null) return;
            ToastBox.Text = msg;
            var inA = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.25));
            var outA = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.6))
            { BeginTime = TimeSpan.FromSeconds(2.0) };
            ToastBox.BeginAnimation(TextBlock.OpacityProperty, inA);
            ToastBox.BeginAnimation(TextBlock.OpacityProperty, outA);
        }

        /// <summary>LIGHTSPEED WARP — for connect / power-cycle / mount.</summary>
        public static void WarpConnect(string caption, Action work, Action after = null)
        {
            SoundService.Instance.Play(SoundService.Clip.Connect);
            RunAfterTransition(caption, work, after, connect: true);
        }

        /// <summary>LEAVING THE UNIVERSE — for eject.</summary>
        public static void WarpEject(string caption, Action work, Action after = null)
        {
            SoundService.Instance.Play(SoundService.Clip.Disconnect);
            RunAfterTransition(caption, work, after, connect: false);
        }

        private static void RunAfterTransition(string caption, Action work, Action after, bool connect)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (Overlay == null)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    try { work?.Invoke(); } finally { dispatcher?.Invoke(() => after?.Invoke()); }
                });
                return;
            }

            Action onComplete = () =>
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    try { work?.Invoke(); } finally { dispatcher?.Invoke(() => after?.Invoke()); }
                });
            };

            if (connect) Overlay.PlayConnect(caption, onComplete);
            else Overlay.PlayEject(caption, onComplete);
        }
    }
}
