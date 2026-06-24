using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DriveUniverse.Controls
{
    /// <summary>
    /// A living deep-space backdrop: drifting twinkling stars plus an optional
    /// "warp" mode that launches radial light streaks from the centre (used for
    /// the connect / power-cycle transition).
    /// </summary>
    public partial class Starfield : UserControl
    {
        private readonly List<Ellipse> _stars = new List<Ellipse>();
        private readonly Random _rnd = new Random();
        private static readonly Brush[] _tints =
        {
            Brushes.White, new SolidColorBrush(Color.FromRgb(0x8A,0xC3,0xFF)),
            new SolidColorBrush(Color.FromRgb(0xFF,0xD9,0x9B)), new SolidColorBrush(Color.FromRgb(0xCF,0xB8,0xFF))
        };

        public Starfield()
        {
            InitializeComponent();
            Loaded += (s, e) => Seed();
            IsHitTestVisible = false;
        }

        private void Seed()
        {
            Field.Children.Clear();
            _stars.Clear();
            int count = (int)(ActualWidth * ActualHeight / 6500);
            for (int i = 0; i < count; i++)
            {
                double size = _rnd.NextDouble() < 0.85 ? 1.2 : 2.4;
                var star = new Ellipse
                {
                    Width = size, Height = size,
                    Fill = _tints[_rnd.Next(_tints.Length)],
                    Opacity = 0.3 + _rnd.NextDouble() * 0.7
                };
                Canvas.SetLeft(star, _rnd.NextDouble() * ActualWidth);
                Canvas.SetTop(star, _rnd.NextDouble() * ActualHeight);
                Twinkle(star);
                Field.Children.Add(star);
                _stars.Add(star);
            }
        }

        private void Twinkle(Ellipse star)
        {
            var dur = TimeSpan.FromSeconds(1.5 + _rnd.NextDouble() * 4);
            var a = new DoubleAnimation(star.Opacity * 0.3, star.Opacity, dur)
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            Storyboard.SetTarget(a, star);
            Storyboard.SetTargetProperty(a, new PropertyPath(Ellipse.OpacityProperty));
            var sb = new Storyboard(); sb.Children.Add(a); sb.Begin();
        }

        /// <summary>Burst radial light streaks from the centre — the warp effect.</summary>
        public void Warp(double durationSeconds = 1.6)
        {
            double cx = ActualWidth / 2, cy = ActualHeight / 2;
            double max = Math.Sqrt(cx * cx + cy * cy) + 60;
            int n = 70;
            for (int i = 0; i < n; i++)
            {
                double ang = _rnd.NextDouble() * Math.PI * 2;
                var streak = new Line
                {
                    Stroke = _tints[_rnd.Next(_tints.Length)],
                    StrokeThickness = _rnd.NextDouble() * 1.6 + 0.4,
                    X1 = cx, Y1 = cy, X2 = cx, Y2 = cy,
                    Opacity = 0
                };
                Field.Children.Add(streak);

                double dx = Math.Cos(ang), dy = Math.Sin(ang);
                var xa = new DoubleAnimation(cx, cx + dx * max, TimeSpan.FromSeconds(durationSeconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                var ya = new DoubleAnimation(cy, cy + dy * max, TimeSpan.FromSeconds(durationSeconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                var x2 = new DoubleAnimation(cx, cx + dx * max * 1.25, TimeSpan.FromSeconds(durationSeconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                var y2 = new DoubleAnimation(cy, cy + dy * max * 1.25, TimeSpan.FromSeconds(durationSeconds)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
                var opIn = new DoubleAnimation(0, 0.9, TimeSpan.FromSeconds(durationSeconds * 0.3));
                var opOut = new DoubleAnimation(0.9, 0, TimeSpan.FromSeconds(durationSeconds * 0.5)) { BeginTime = TimeSpan.FromSeconds(durationSeconds * 0.5) };

                var sb = new Storyboard();
                sb.Children.Add(xa); sb.Children.Add(ya); sb.Children.Add(x2); sb.Children.Add(y2);
                sb.Children.Add(opIn); sb.Children.Add(opOut);
                Storyboard.SetTarget(xa, streak); Storyboard.SetTargetProperty(xa, new PropertyPath(Line.X1Property));
                Storyboard.SetTarget(ya, streak); Storyboard.SetTargetProperty(ya, new PropertyPath(Line.Y1Property));
                Storyboard.SetTarget(x2, streak); Storyboard.SetTargetProperty(x2, new PropertyPath(Line.X2Property));
                Storyboard.SetTarget(y2, streak); Storyboard.SetTargetProperty(y2, new PropertyPath(Line.Y2Property));
                Storyboard.SetTarget(opIn, streak); Storyboard.SetTargetProperty(opIn, new PropertyPath(Line.OpacityProperty));
                Storyboard.SetTarget(opOut, streak); Storyboard.SetTargetProperty(opOut, new PropertyPath(Line.OpacityProperty));
                sb.Completed += (s, e) => Field.Children.Remove(streak);
                sb.Begin();
            }
        }
    }
}
