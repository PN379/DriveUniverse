using System;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DriveUniverse.Services;

namespace DriveUniverse
{
    /// <summary>
    /// Floating Orbit widget — a small draggable circle on the desktop.
    ///
    /// - DRAG to move it anywhere (threshold: must move >5px to count as drag)
    /// - CLICK Orbit (no drag) to open the main panel
    /// - HOVER: app icons appear in a radial ring around Orbit (click to launch)
    /// - QUICK BUTTONS: eject + mount/cycle below the sphere (toggleable in settings)
    /// - Voice command: text grows below, ring pulses green/red, auto-fades
    /// </summary>
    public partial class OrbitWidget : Window
    {
        private bool _isDragging;
        private Point _dragStartScreen;
        private double _dragStartLeft, _dragStartTop;
        private const double DragThreshold = 5.0;
        private DispatcherTimer _ringPulseTimer;

        /// <summary>The currently selected drive letter (set by MainWindow).</summary>
        public char? CurrentDriveLetter { get; set; }

        /// <summary>The currently selected off-device instance (for power-cycle).</summary>
        public string CurrentOffInstance { get; set; }

        public event Action OpenMainRequested;

        // Quick action events — MainWindow handles the actual drive operations
        public event Action QuickEjectRequested;
        public event Action QuickCycleRequested;

        public OrbitWidget()
        {
            InitializeComponent();

            MouseEnter += (s, e) => OnHover(true);
            MouseLeave += (s, e) => OnHover(false);

            _ringPulseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.0) };
            _ringPulseTimer.Tick += (s, e) => PulseRingIdle();
            _ringPulseTimer.Start();

            // Quick buttons start hidden — they only appear on hover (if preference allows)
            QuickActions.Opacity = 0;
            UpdateQuickActionsVisibility();
        }

        /// <summary>
        /// Controls whether quick actions CAN appear (preference). When on, they're
        /// in the tree but hidden (opacity 0) until hover. When off, collapsed entirely.
        /// </summary>
        public void UpdateQuickActionsVisibility()
        {
            bool allowed = LauncherService.Instance.ShowWidgetQuickActions;
            QuickActions.Visibility = allowed ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---- drag vs click detection ----

        private void HitArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _dragStartScreen = PointToScreen(e.GetPosition(this));
            _dragStartLeft = Left;
            _dragStartTop = Top;
            HitArea.CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && HitArea.IsMouseCaptured)
            {
                var screenPos = PointToScreen(e.GetPosition(this));
                double dx = screenPos.X - _dragStartScreen.X;
                double dy = screenPos.Y - _dragStartScreen.Y;

                if (!_isDragging && Math.Abs(dx) + Math.Abs(dy) > DragThreshold)
                    _isDragging = true;

                if (_isDragging)
                {
                    Left = _dragStartLeft + dx;
                    Top = _dragStartTop + dy;
                }
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (HitArea.IsMouseCaptured) HitArea.ReleaseMouseCapture();
            if (!_isDragging)
            {
                OpenMainRequested?.Invoke();
            }
            _isDragging = false;
            base.OnMouseLeftButtonUp(e);
        }

        // ---- quick actions ----

        private void QuickEject_Click(object s, RoutedEventArgs e)
        {
            QuickEjectRequested?.Invoke();
            Ui.Sound(SoundService.Clip.Click);
        }

        private void QuickCycle_Click(object s, RoutedEventArgs e)
        {
            QuickCycleRequested?.Invoke();
            Ui.Sound(SoundService.Clip.Click);
        }

        // ---- hover radial menu ----

        private void OnHover(bool on)
        {
            if (on) BuildRadialMenu();

            var anim = new DoubleAnimation(on ? 0 : 1, on ? 1 : 0, TimeSpan.FromSeconds(0.3));
            RadialMenu.BeginAnimation(OpacityProperty, anim);

            // Quick buttons fade in/out with hover too (only if preference allows them)
            if (QuickActions.Visibility == Visibility.Visible)
            {
                var qaAnim = new DoubleAnimation(on ? 0 : 1, on ? 1 : 0, TimeSpan.FromSeconds(0.3))
                { BeginTime = on ? TimeSpan.FromSeconds(0.1) : TimeSpan.Zero };
                QuickActions.BeginAnimation(OpacityProperty, qaAnim);
            }
        }

        private void BuildRadialMenu()
        {
            RadialMenu.Children.Clear();
            var apps = LauncherService.Instance.Apps;
            if (apps.Count == 0) return;

            double cx = 100, cy = 85;  // center of the sphere (upper portion)
            double radius = 68;
            int count = apps.Count;
            for (int i = 0; i < count && i < 8; i++)
            {
                double angle = (Math.PI * 2 * i / count) - Math.PI / 2;
                double x = cx + Math.Cos(angle) * radius - 16;
                double y = cy + Math.Sin(angle) * radius - 16;

                var app = apps[i];

                var btn = new Border
                {
                    Width = 32, Height = 32, CornerRadius = new CornerRadius(9),
                    Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0D, 0x12, 0x1F)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x45, 0xE7, 0xF7)),
                    BorderThickness = new Thickness(1.2),
                    Tag = app,
                    Cursor = Cursors.Hand
                };
                btn.Effect = new System.Windows.Media.Effects.DropShadowEffect
                { Color = Color.FromRgb(0x45, 0xE7, 0xF7), BlurRadius = 8, ShadowDepth = 0, Opacity = 0.4 };

                var grid = new Grid();
                if (app.AppIcon != null)
                {
                    var img = new Image
                    {
                        Source = app.AppIcon, Width = 24, Height = 24,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    grid.Children.Add(img);
                }
                else
                {
                    grid.Children.Add(new TextBlock
                    {
                        Text = app.Glyph, FontSize = 16,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFD, 0xC5, 0x5E)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                btn.Child = grid;

                btn.MouseEnter += (s, e) =>
                {
                    btn.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x14, 0x1B, 0x2E));
                    btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0x45, 0xE7, 0xF7));
                };
                btn.MouseLeave += (s, e) =>
                {
                    btn.Background = new SolidColorBrush(Color.FromArgb(0xDD, 0x0D, 0x12, 0x1F));
                    btn.BorderBrush = new SolidColorBrush(Color.FromArgb(0x88, 0x45, 0xE7, 0xF7));
                };
                btn.MouseLeftButtonUp += (s, e) =>
                {
                    LauncherService.Instance.Launch(app);
                    Ui.Toast("Launched " + app.Name);
                    Ui.Sound(SoundService.Clip.Click);
                    OnHover(false);
                };

                Canvas.SetLeft(btn, x);
                Canvas.SetTop(btn, y);
                RadialMenu.Children.Add(btn);
            }
        }

        // ---- idle ring pulse ----

        private void PulseRingIdle()
        {
            var pulse = new DoubleAnimation(0.3, 0.65, TimeSpan.FromSeconds(1.0))
            { AutoReverse = true, EasingFunction = new SineEase() };
            GlowRing.BeginAnimation(OpacityProperty, pulse);
        }

        // ---- command feedback ----

        /// <summary>Show a confirmation prompt with amber ring pulse.</summary>
        public void ShowConfirm(string text)
        {
            Dispatcher.Invoke(() =>
            {
                CommandText.Text = text;
                CommandText.Foreground = new SolidColorBrush(Color.FromRgb(0xFD, 0xC5, 0x5E));
                CmdBorderBrush.Color = Color.FromRgb(0xFD, 0xC5, 0x5E);
                RingColor.Color = Color.FromArgb(0x00, 0xFD, 0xC5, 0x5E);
                RingOuter.Color = Color.FromArgb(0x99, 0xFD, 0xC5, 0x5E);

                var cmdIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
                CommandBox.BeginAnimation(OpacityProperty, cmdIn);

                var ringPulse = new DoubleAnimation(0.5, 1.0, TimeSpan.FromSeconds(0.6))
                { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                GlowRing.BeginAnimation(OpacityProperty, ringPulse);
            });
        }

        /// <summary>Clear any prompt (confirmation or command).</summary>
        public void ClearPrompt()
        {
            Dispatcher.Invoke(() =>
            {
                var cmdOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.4));
                CommandBox.BeginAnimation(OpacityProperty, cmdOut);
                RingColor.Color = Color.FromArgb(0x00, 0x45, 0xE7, 0xF7);
                RingOuter.Color = Color.FromArgb(0x66, 0x45, 0xE7, 0xF7);
            });
        }

        public void ShowCommand(string text, bool success)
        {
            Dispatcher.Invoke(() =>
            {
                CommandText.Text = text;
                CommandText.Foreground = success
                    ? new SolidColorBrush(Color.FromRgb(0x58, 0xED, 0x9D))
                    : new SolidColorBrush(Color.FromRgb(0xFF, 0x70, 0x88));

                CmdBorderBrush.Color = success
                    ? Color.FromRgb(0x58, 0xED, 0x9D)
                    : Color.FromRgb(0xFF, 0x70, 0x88);

                RingColor.Color = success
                    ? Color.FromArgb(0x00, 0x58, 0xED, 0x9D)
                    : Color.FromArgb(0x00, 0xFF, 0x70, 0x88);
                RingOuter.Color = success
                    ? Color.FromArgb(0x99, 0x58, 0xED, 0x9D)
                    : Color.FromArgb(0x99, 0xFF, 0x70, 0x88);

                var cmdIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
                CommandBox.BeginAnimation(OpacityProperty, cmdIn);

                var ringPulse = new DoubleAnimation(0.5, 1.0, TimeSpan.FromSeconds(0.5))
                { AutoReverse = !success, RepeatBehavior = success ? new RepeatBehavior(2) : RepeatBehavior.Forever };
                GlowRing.BeginAnimation(OpacityProperty, ringPulse);

                if (success) Mascot.Happy(); else Mascot.UhOh();

                var fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                fadeTimer.Tick += (s, e) =>
                {
                    var cmdOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.6));
                    CommandBox.BeginAnimation(OpacityProperty, cmdOut);
                    RingColor.Color = Color.FromArgb(0x00, 0x45, 0xE7, 0xF7);
                    RingOuter.Color = Color.FromArgb(0x66, 0x45, 0xE7, 0xF7);
                    fadeTimer.Stop();
                };
                fadeTimer.Start();
            });
        }
        // ---- mini warp/eject animation inside the widget circle ----

        private readonly Random _fxRnd = new Random();

        /// <summary>Play mini eject: Orbit bounces, fades, planet departs, fades back.</summary>
        public void PlayMiniEject(Action onComplete = null)
        {
            Dispatcher.Invoke(() =>
            {
                SoundService.Instance.Play(SoundService.Clip.Disconnect);
                Mascot.Happy();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.4));
                fadeOut.Completed += (s, e) =>
                {
                    SpawnMiniDeparture();
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5)) { BeginTime = TimeSpan.FromSeconds(2.0) };
                    fadeIn.Completed += (s2, e2) => onComplete?.Invoke();
                    Mascot.BeginAnimation(OpacityProperty, fadeIn);
                };
                Mascot.BeginAnimation(OpacityProperty, fadeOut);
            });
        }

        /// <summary>Play mini connect: Orbit fades, warp streaks burst, fades back.</summary>
        public void PlayMiniConnect(Action onComplete = null)
        {
            Dispatcher.Invoke(() =>
            {
                SoundService.Instance.Play(SoundService.Clip.Connect);
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
                fadeOut.Completed += (s, e) =>
                {
                    SpawnMiniWarp();
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5)) { BeginTime = TimeSpan.FromSeconds(1.5) };
                    fadeIn.Completed += (s2, e2) => { Mascot.Happy(); onComplete?.Invoke(); };
                    Mascot.BeginAnimation(OpacityProperty, fadeIn);
                };
                Mascot.BeginAnimation(OpacityProperty, fadeOut);
            });
        }

        private void SpawnMiniDeparture()
        {
            double cx = 44, cy = 44;
            var planet = new Ellipse
            {
                Width = 30, Height = 30,
                Fill = new RadialGradientBrush(Color.FromRgb(0x6B, 0xEA, 0xF8), Color.FromRgb(0x1E, 0x5F, 0xA8)),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Color.FromRgb(0x45, 0xE7, 0xF7), BlurRadius = 12, ShadowDepth = 0, Opacity = 0.6 }
            };
            Canvas.SetLeft(planet, cx - 15);
            Canvas.SetTop(planet, cy - 15);
            FxCanvas.Children.Add(planet);
            var sc = new ScaleTransform(1, 1, 15, 15);
            planet.RenderTransform = sc;
            var scX = new DoubleAnimation(1, 0.05, TimeSpan.FromSeconds(1.8)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            var scY = new DoubleAnimation(1, 0.05, TimeSpan.FromSeconds(1.8)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, scX);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, scY);
            var opFade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(1.8)) { BeginTime = TimeSpan.FromSeconds(0.5) };
            planet.BeginAnimation(OpacityProperty, opFade);
            var removeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.1) };
            removeTimer.Tick += (s, e) => { FxCanvas.Children.Remove(planet); removeTimer.Stop(); };
            removeTimer.Start();
        }

        private void SpawnMiniWarp()
        {
            double cx = 44, cy = 44, max = 50;
            for (int i = 0; i < 24; i++)
            {
                double ang = _fxRnd.NextDouble() * Math.PI * 2;
                double dx = Math.Cos(ang), dy = Math.Sin(ang);
                var streak = new System.Windows.Shapes.Line
                {
                    Stroke = i % 2 == 0 ? Brushes.Cyan : Brushes.White,
                    StrokeThickness = _fxRnd.NextDouble() * 1.2 + 0.3,
                    X1 = cx, Y1 = cy, X2 = cx, Y2 = cy, Opacity = 0
                };
                FxCanvas.Children.Add(streak);
                var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };
                streak.BeginAnimation(System.Windows.Shapes.Line.X1Property, new DoubleAnimation(cx, cx + dx * max, TimeSpan.FromSeconds(1.2)) { EasingFunction = ease });
                streak.BeginAnimation(System.Windows.Shapes.Line.Y1Property, new DoubleAnimation(cy, cy + dy * max, TimeSpan.FromSeconds(1.2)) { EasingFunction = ease });
                streak.BeginAnimation(System.Windows.Shapes.Line.X2Property, new DoubleAnimation(cx, cx + dx * max * 1.3, TimeSpan.FromSeconds(1.2)) { EasingFunction = ease });
                streak.BeginAnimation(System.Windows.Shapes.Line.Y2Property, new DoubleAnimation(cy, cy + dy * max * 1.3, TimeSpan.FromSeconds(1.2)) { EasingFunction = ease });
                streak.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 0.9, TimeSpan.FromSeconds(0.2)));
                streak.BeginAnimation(OpacityProperty, new DoubleAnimation(0.9, 0, TimeSpan.FromSeconds(0.4)) { BeginTime = TimeSpan.FromSeconds(0.4) });
                var rt = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0) };
                rt.Tick += (s, e) => { FxCanvas.Children.Remove(streak); rt.Stop(); };
                rt.Start();
            }
        }
    }
}
