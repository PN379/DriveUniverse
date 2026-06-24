using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DriveUniverse.Controls
{
    /// <summary>
    /// Two distinct cinematic transitions:
    ///
    ///  Connect  →  LIGHTSPEED WARP: a hyperspace burst of light streaks flying
    ///              outward from the centre. Fast, energetic, cyan. No receding world.
    ///
    ///  Eject    →  LEAVING THE UNIVERSE: the glowing planet shrinks and recedes
    ///              into pure blackness while stars drift outward. Slower, departure.
    /// </summary>
    public partial class WarpOverlay : UserControl
    {
        private readonly Random _rnd = new Random();

        public WarpOverlay()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;
        }

        /// <summary>LIGHTSPEED WARP — used for connect / power-cycle / mount.</summary>
        public void PlayConnect(string caption, Action onComplete)
        {
            World.Visibility = Visibility.Collapsed;   // no planet for warp
            Label.Text = caption;
            Visibility = Visibility.Visible;
            IsHitTestVisible = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                double cx = ActualWidth / 2, cy = ActualHeight / 2;
                LightspeedWarp(cx, cy, onComplete);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>LEAVING THE UNIVERSE — used for eject.</summary>
        public void PlayEject(string caption, Action onComplete)
        {
            World.Visibility = Visibility.Visible;     // planet recedes
            Label.Text = caption;
            Visibility = Visibility.Visible;
            IsHitTestVisible = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                double cx = ActualWidth / 2, cy = ActualHeight / 2;
                Departure(cx, cy, onComplete);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // ===== CONNECT: lightspeed hyperspace burst ============================

        private void LightspeedWarp(double cx, double cy, Action onComplete)
        {
            double total = 1.5;
            var sb = new Storyboard();

            // fade in fast
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.15));
            Storyboard.SetTarget(fadeIn, this);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
            sb.Children.Add(fadeIn);

            // label quick flash
            var labIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.2));
            var labOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.4)) { BeginTime = TimeSpan.FromSeconds(0.8) };
            Storyboard.SetTarget(labIn, Label); Storyboard.SetTargetProperty(labIn, new PropertyPath(OpacityProperty));
            Storyboard.SetTarget(labOut, Label); Storyboard.SetTargetProperty(labOut, new PropertyPath(OpacityProperty));
            sb.Children.Add(labIn); sb.Children.Add(labOut);

            sb.Completed += (s, e) =>
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.35));
                fadeOut.Completed += (s2, e2) => { Cleanup(); onComplete?.Invoke(); };
                this.BeginAnimation(OpacityProperty, fadeOut);
            };

            // MANY streaks, fast, thick — true hyperspace feel
            SpawnStreaks(cx, cy, total, count: 120, speed: 1.3, thickness: 2.2, radial: true);
            sb.Begin();
        }

        // ===== EJECT: the world recedes into blackness =========================

        private void Departure(double cx, double cy, Action onComplete)
        {
            double total = 3.0;
            var sb = new Storyboard();

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            Storyboard.SetTarget(fadeIn, this);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
            sb.Children.Add(fadeIn);

            // DRIVE PLANET DETACHES and floats upward into the void
            var driftY = new DoubleAnimation(0, -ActualHeight * 0.6, TimeSpan.FromSeconds(total))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            Storyboard.SetTarget(driftY, WorldTranslate);
            Storyboard.SetTargetProperty(driftY, new PropertyPath(TranslateTransform.YProperty));
            sb.Children.Add(driftY);

            var scX = new DoubleAnimation(1, 0.05, TimeSpan.FromSeconds(total))
            { BeginTime = TimeSpan.FromSeconds(0.8), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            var scY = new DoubleAnimation(1, 0.05, TimeSpan.FromSeconds(total))
            { BeginTime = TimeSpan.FromSeconds(0.8), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            Storyboard.SetTarget(scX, WorldScale); Storyboard.SetTargetProperty(scX, new PropertyPath(ScaleTransform.ScaleXProperty));
            Storyboard.SetTarget(scY, WorldScale); Storyboard.SetTargetProperty(scY, new PropertyPath(ScaleTransform.ScaleYProperty));
            sb.Children.Add(scX); sb.Children.Add(scY);

            var glowFade = new DoubleAnimation(0.6, 0, TimeSpan.FromSeconds(total))
            { BeginTime = TimeSpan.FromSeconds(1.0) };
            Storyboard.SetTarget(glowFade, WorldGlow);
            Storyboard.SetTargetProperty(glowFade, new PropertyPath(DropShadowEffect.OpacityProperty));
            sb.Children.Add(glowFade);

            var ringRot = new DoubleAnimation(-18, 160, TimeSpan.FromSeconds(total));
            Storyboard.SetTarget(ringRot, WorldRing);
            Storyboard.SetTargetProperty(ringRot, new PropertyPath(RotateTransform.AngleProperty));
            sb.Children.Add(ringRot);

            var labIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.6)) { BeginTime = TimeSpan.FromSeconds(1.2) };
            var labOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5)) { BeginTime = TimeSpan.FromSeconds(total - 0.5) };
            Storyboard.SetTarget(labIn, Label); Storyboard.SetTargetProperty(labIn, new PropertyPath(OpacityProperty));
            Storyboard.SetTarget(labOut, Label); Storyboard.SetTargetProperty(labOut, new PropertyPath(OpacityProperty));
            sb.Children.Add(labIn); sb.Children.Add(labOut);

            sb.Completed += (s, e) =>
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.6));
                fadeOut.Completed += (s2, e2) => { Cleanup(); onComplete?.Invoke(); };
                this.BeginAnimation(OpacityProperty, fadeOut);
            };

            SpawnStreaks(cx, cy, total, count: 20, speed: 0.3, thickness: 0.8, radial: true);
            sb.Begin();
        }

        // ===== streak generator =================================================

        private void SpawnStreaks(double cx, double cy, double dur, int count, double speed, double thickness, bool radial)
        {
            double max = (Math.Sqrt(cx * cx + cy * cy) + 80) * speed;
            for (int i = 0; i < count; i++)
            {
                double ang = _rnd.NextDouble() * Math.PI * 2;
                double dx = Math.Cos(ang), dy = Math.Sin(ang);
                double len = (_rnd.NextDouble() * 0.6 + 0.4) * max;  // variable length per streak

                var ln = new Line
                {
                    Stroke = i % 3 == 0 ? Brushes.Cyan : (i % 3 == 1 ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x8E, 0x70, 0xFF))),
                    StrokeThickness = _rnd.NextDouble() * thickness + 0.3,
                    X1 = cx, Y1 = cy, X2 = cx, Y2 = cy, Opacity = 0
                };
                Streaks.Children.Add(ln);

                var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };
                // streak starts slightly off-centre (tail) and flies outward
                double startOff = _rnd.NextDouble() * 0.2 * max;
                double x1From = cx + dx * startOff, y1From = cy + dy * startOff;
                double x1To = cx + dx * len, y1To = cy + dy * len;
                double x2To = cx + dx * len * 1.35, y2To = cy + dy * len * 1.35;

                var x1 = new DoubleAnimation(x1From, x1To, TimeSpan.FromSeconds(dur)) { EasingFunction = ease };
                var y1 = new DoubleAnimation(y1From, y1To, TimeSpan.FromSeconds(dur)) { EasingFunction = ease };
                var x2 = new DoubleAnimation(cx, x2To, TimeSpan.FromSeconds(dur)) { EasingFunction = ease };
                var y2 = new DoubleAnimation(cy, y2To, TimeSpan.FromSeconds(dur)) { EasingFunction = ease };
                var o1 = new DoubleAnimation(0, 0.9, TimeSpan.FromSeconds(dur * 0.25));
                var o2 = new DoubleAnimation(0.9, 0, TimeSpan.FromSeconds(dur * 0.45)) { BeginTime = TimeSpan.FromSeconds(dur * 0.55) };

                var s = new Storyboard();
                s.Children.Add(x1); s.Children.Add(y1); s.Children.Add(x2); s.Children.Add(y2); s.Children.Add(o1); s.Children.Add(o2);
                Storyboard.SetTarget(x1, ln); Storyboard.SetTargetProperty(x1, new PropertyPath(Line.X1Property));
                Storyboard.SetTarget(y1, ln); Storyboard.SetTargetProperty(y1, new PropertyPath(Line.Y1Property));
                Storyboard.SetTarget(x2, ln); Storyboard.SetTargetProperty(x2, new PropertyPath(Line.X2Property));
                Storyboard.SetTarget(y2, ln); Storyboard.SetTargetProperty(y2, new PropertyPath(Line.Y2Property));
                Storyboard.SetTarget(o1, ln); Storyboard.SetTargetProperty(o1, new PropertyPath(Line.OpacityProperty));
                Storyboard.SetTarget(o2, ln); Storyboard.SetTargetProperty(o2, new PropertyPath(Line.OpacityProperty));
                s.Completed += (se, ev) => Streaks.Children.Remove(ln);
                s.Begin();
            }
        }

        private void Cleanup()
        {
            Visibility = Visibility.Collapsed;
            IsHitTestVisible = false;
            Streaks.Children.Clear();
            Particles.Children.Clear();
            WorldScale.ScaleX = 1; WorldScale.ScaleY = 1;
            WorldTranslate.X = 0; WorldTranslate.Y = 0;
            World.Opacity = 1;
            WorldGlow.Opacity = 0.6;
        }
    }
}
