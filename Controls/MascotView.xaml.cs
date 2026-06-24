using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DriveUniverse.Controls
{
    /// <summary>
    /// "Orbit" — a fully vector-drawn, lively planet mascot with orbiting satellite,
    /// blinking eyes, breathing glow, cheek blush, and expressive reactions.
    /// </summary>
    public partial class MascotView : UserControl
    {
        private DispatcherTimer _blinkTimer;
        private DispatcherTimer _lookTimer;
        private readonly Random _rnd = new Random();

        public MascotView()
        {
            InitializeComponent();
            Loaded += (s, e) => StartIdle();
        }

        private void StartIdle()
        {
            // gentle breathing
            var sx = new DoubleAnimation(0.98, 1.03, TimeSpan.FromSeconds(4.0))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            var sy = new DoubleAnimation(1.03, 0.98, TimeSpan.FromSeconds(4.0))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            Breath.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            Breath.BeginAnimation(ScaleTransform.ScaleYProperty, sy);

            // subtle float
            var lift = new DoubleAnimation(-2, 3, TimeSpan.FromSeconds(5.0))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            Lift.BeginAnimation(TranslateTransform.YProperty, lift);

            // orbit ring + satellite spin
            var ringRot = new DoubleAnimation(-18, 342, TimeSpan.FromSeconds(12.0))
            { RepeatBehavior = RepeatBehavior.Forever };
            RingRot.BeginAnimation(RotateTransform.AngleProperty, ringRot);

            // halo slow pulse
            var halo = new DoubleAnimation(0.25, 0.5, TimeSpan.FromSeconds(3.5))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            Halo.BeginAnimation(OpacityProperty, halo);

            // random blinking
            _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2 + _rnd.Next(4)) };
            _blinkTimer.Tick += (s, e) => { Blink(); _blinkTimer.Interval = TimeSpan.FromSeconds(2 + _rnd.Next(5)); };
            _blinkTimer.Start();

            // occasional look-around (pupils drift)
            _lookTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4 + _rnd.Next(6)) };
            _lookTimer.Tick += (s, e) => { LookAround(); _lookTimer.Interval = TimeSpan.FromSeconds(5 + _rnd.Next(8)); };
            _lookTimer.Start();
        }

        private void Blink()
        {
            var close = new DoubleAnimationUsingKeyFrames();
            close.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            close.KeyFrames.Add(new LinearDoubleKeyFrame(0.1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
            close.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
            EyeLScale.BeginAnimation(ScaleTransform.ScaleYProperty, close);

            var close2 = new DoubleAnimationUsingKeyFrames();
            close2.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            close2.KeyFrames.Add(new LinearDoubleKeyFrame(0.1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
            close2.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
            EyeRScale.BeginAnimation(ScaleTransform.ScaleYProperty, close2);
        }

        private void LookAround()
        {
            // pupils drift slightly in a random direction then back
            double dx = (_rnd.NextDouble() - 0.5) * 4;
            double dy = (_rnd.NextDouble() - 0.5) * 3;
            var lookX = new DoubleAnimationUsingKeyFrames();
            lookX.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            lookX.KeyFrames.Add(new EasingDoubleKeyFrame(dx, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600))) { EasingFunction = new SineEase() });
            lookX.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.8))) { EasingFunction = new SineEase() });

            var lookY = new DoubleAnimationUsingKeyFrames();
            lookY.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            lookY.KeyFrames.Add(new EasingDoubleKeyFrame(dy, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600))) { EasingFunction = new SineEase() });
            lookY.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.8))) { EasingFunction = new SineEase() });

            var tl = new TranslateTransform();
            PupilL.RenderTransform = tl;
            tl.BeginAnimation(TranslateTransform.XProperty, lookX);
            tl.BeginAnimation(TranslateTransform.YProperty, lookY);

            var tr = new TranslateTransform();
            PupilR.RenderTransform = tr;
            tr.BeginAnimation(TranslateTransform.XProperty, lookX);
            tr.BeginAnimation(TranslateTransform.YProperty, lookY);
        }

        public void SetListening(bool on)
        {
            var halo = new DoubleAnimation(on ? 0.5 : 0.35, on ? 0.9 : 0.35, TimeSpan.FromSeconds(0.4));
            Halo.BeginAnimation(OpacityProperty, halo);
            HaloCore.Color = on ? Color.FromRgb(0x58, 0xED, 0x9D) : Color.FromRgb(0x45, 0xE7, 0xF7);

            if (on)
            {
                var dot = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
                ListenDot.BeginAnimation(OpacityProperty, dot);
                var pulse = new DoubleAnimation(0.5, 1.0, TimeSpan.FromSeconds(0.7))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                ListenDot.BeginAnimation(OpacityProperty, pulse);
            }
            else
            {
                var dot = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
                ListenDot.BeginAnimation(OpacityProperty, dot);
            }
        }

        public void Happy()
        {
            // happy bounce + blink
            var q = new DoubleAnimationUsingKeyFrames();
            q.KeyFrames.Add(new LinearDoubleKeyFrame(-10, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.10))));
            q.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.32))) { EasingFunction = new BounceEase() });
            Lift.BeginAnimation(TranslateTransform.YProperty, q);
            Blink();

            // smile widens briefly
            var wide = new DoubleAnimationUsingKeyFrames();
            wide.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            wide.KeyFrames.Add(new LinearDoubleKeyFrame(1.2, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.15))));
            wide.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4))));
            Breath.BeginAnimation(ScaleTransform.ScaleXProperty, wide);
        }

        public void UhOh()
        {
            // shake
            var q = new DoubleAnimationUsingKeyFrames();
            q.KeyFrames.Add(new LinearDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
            q.KeyFrames.Add(new LinearDoubleKeyFrame(5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
            q.KeyFrames.Add(new LinearDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(210))));
            q.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));
            Lift.BeginAnimation(TranslateTransform.YProperty, q);
        }
    }
}
