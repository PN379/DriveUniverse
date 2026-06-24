using System;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DriveUniverse.Controls;
using DriveUniverse.Services;
using DriveUniverse.Voice;
using Application = System.Windows.Application;

namespace DriveUniverse
{
    public partial class MainWindow : Window
    {
        private NotifyIcon _tray;
        private ContextMenuStrip _menu;
        private bool _devMode;
        private string _lastNav = "drive";
        private OrbitWidget _orbitWidget;

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) => PositionNearTray();
            Loaded += (s, e) => UpdateClip();
            SizeChanged += (s, e) => UpdateClip();
        }

        private void UpdateClip()
        {
            if (Chrome.Child is System.Windows.Controls.Grid grid)
            {
                grid.Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), 18, 18);
            }
        }

        public void Boot(bool safe = false)
        {
            try
            {
                LogService.Log("Boot: InitializeComponent (already done in ctor).");
                PositionNearTray();

                LogService.Log("Boot: Host already initialised by App.");

                LogService.Log("Boot: WireUi()...");
                WireUi();
                LogService.Log("Boot: WireUi() OK.");

                if (!safe)
                {
                    LogService.Log("Boot: BuildTray()...");
                    BuildTray();
                    LogService.Log("Boot: BuildTray() OK.");
                }

                LogService.Log("Boot: WireVoice()...");
                WireVoice();
                LogService.Log("Boot: WireVoice() OK.");

                try
                {
                    var drives = DriveService.GetDrives();
                    LogService.Log("Boot: found " + drives.Count + " drive(s).");
                    // Point monitor at the first USB drive (not C:)
                    var usbDrive = drives.FirstOrDefault(d => d.IsUsb && !d.IsSystem);
                    if (usbDrive != null) Host.Monitor.DriveLetter = usbDrive.Letter;
                }
                catch (Exception ex) { LogService.Log("Boot: drive enumeration warning: " + ex.Message); }

                ShowNav("drive");
                LogService.Log("Boot: complete.");

                // Wire the Settings "Open Dev Console" button
                ViewSet.OpenDevConsole += () => Dispatcher.Invoke(() => DevToggle_Click(null, null));

                // Create the floating Orbit widget (HIDDEN at start — toggle via button)
                try
                {
                    _orbitWidget = new OrbitWidget();
                    _orbitWidget.OpenMainRequested += () => Dispatcher.Invoke(() => { PositionNearTray(); Show(); Activate(); });
                    _orbitWidget.QuickEjectRequested += () => Widget_QuickEject();
                    _orbitWidget.QuickCycleRequested += () => Widget_QuickCycle();
                    // Position at bottom-left as requested
                    var wa = SystemParameters.WorkArea;
                    _orbitWidget.Left = wa.Left + 20;
                    _orbitWidget.Top = wa.Bottom - 280;
                    _orbitWidget.Hide();
                    LogService.Log("Boot: OrbitWidget created (hidden).");

                if (Host.VoiceReady)
                {
                    if (LauncherService.Instance.VoiceEnabled && LauncherService.Instance.AlwaysListening)
                    {
                        Host.Voice.StartAlwaysListening();
                        LogService.Log("Boot: always-listening auto-started.");
                    }
                }
                }
                catch (Exception ex) { LogService.Log("OrbitWidget init error: " + ex.Message); }
            }
            catch (Exception ex) { LogService.Log("Boot FAILED: " + ex); }
        }

        private void WireUi()
        {
            Ui.Overlay = Warp;
            Ui.Mascot = Mascot;
            Ui.ToastBox = Toast;
            Ui.Logger = line => Dispatcher.Invoke(() => ViewDev.Print(line));
            Ui.ResultCallback = (msg, success) => Dispatcher.Invoke(() =>
            {
                _orbitWidget?.ShowCommand(msg, success);
            });
            SoundService.Instance.Enabled = LauncherService.Instance.SoundsEnabled;
        }

        // ---- tray ----
        private void BuildTray()
        {
            _tray = new NotifyIcon { Icon = MakeTrayIcon(), Visible = true, Text = "DriveUniverse" };
            _tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleWindow(); };

            _menu = new ContextMenuStrip();
            _menu.Items.Add("Open DriveUniverse", null, (s, e) => ToggleWindow());
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Drive Control", null, (s, e) => { ShowNav("drive"); Show(); });
            _menu.Items.Add("Processes", null, (s, e) => { ShowNav("proc"); Show(); });
            _menu.Items.Add("Rules", null, (s, e) => { ShowNav("rules"); Show(); });
            _menu.Items.Add("Apps", null, (s, e) => { ShowNav("apps"); Show(); });
            _menu.Items.Add("Voice", null, (s, e) => { ShowNav("voice"); Show(); });
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Exit", null, (s, e) =>
            {
                _tray.Visible = false;
                Host.Shutdown();
                Application.Current.Shutdown();
            });
            _tray.ContextMenuStrip = _menu;
        }

        private Icon MakeTrayIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/app_icon.png");
                var ri = Application.GetResourceStream(uri);
                if (ri != null)
                {
                    var img = System.Drawing.Image.FromStream(ri.Stream);
                    var bmp = new Bitmap(32, 32);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.DrawImage(img, 0, 0, 32, 32);
                    }
                    return System.Drawing.Icon.FromHandle(bmp.GetHicon());
                }
            }
            catch { }
            try
            {
                var bmp = new Bitmap(32, 32);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(System.Drawing.Color.Transparent);
                    using (var b = new SolidBrush(System.Drawing.Color.FromArgb(59, 224, 240)))
                        g.FillEllipse(b, 5, 5, 22, 22);
                    using (var b = new SolidBrush(System.Drawing.Color.FromArgb(138, 107, 255)))
                        g.FillEllipse(b, 14, 6, 14, 14);
                }
                return System.Drawing.Icon.FromHandle(bmp.GetHicon());
            }
            catch { return SystemIcons.Application; }
        }

        // ---- window ----
        private void ToggleWindow()
        {
            if (IsVisible) Hide();
            else { PositionNearTray(); Show(); Activate(); }
        }

        private void PositionNearTray()
        {
            try
            {
                var wa = SystemParameters.WorkArea;
                Top = wa.Bottom - Height - 6;
                Left = wa.Right - Width - 6;
                if (Top < 0) Top = 0;
                if (Left < 0) Left = 0;
            }
            catch { }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e) { e.Cancel = true; Hide(); }
        private void Close_Click(object s, RoutedEventArgs e)
        {
            // Close = exit the app entirely
            try { _tray.Visible = false; } catch { }
            Host.Shutdown();
            System.Windows.Application.Current.Shutdown();
        }
        private void Minimize_Click(object s, RoutedEventArgs e) => Hide();

        private void Mascot_Click(object s, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Clicking Orbit (the mascot) toggles the floating widget
            OrbitWidgetToggle_Click(null, null);
        }

        private void OrbitWidgetToggle_Click(object s, RoutedEventArgs e)
        {
            if (_orbitWidget == null) return;
            if (_orbitWidget.IsVisible)
            {
                _orbitWidget.Hide();
                Ui.Toast("Orbit widget hidden");
            }
            else
            {
                _orbitWidget.Show();
                _orbitWidget.UpdateQuickActionsVisibility();
                // Minimize the tray when widget opens
                Hide();
                Ui.Sound(SoundService.Clip.Confirm);
            }
        }

        // ---- widget quick action handlers ----

        private void Widget_QuickEject()
        {
            _orbitWidget?.PlayMiniEject(() => Dispatcher.Invoke(() => ViewDrive.Eject_Click(null, null)));
        }

        private void Widget_QuickCycle()
        {
            _orbitWidget?.PlayMiniConnect(() => Dispatcher.Invoke(() => ViewDrive.Power_Click(null, null)));
        }

        // ---- sync widget drive selection ----

        public void SyncWidgetDrive(char? letter, string offInstance)
        {
            if (_orbitWidget == null) return;
            _orbitWidget.CurrentDriveLetter = letter;
            _orbitWidget.CurrentOffInstance = offInstance;
        }
        private void Chrome_Drag(object s, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) DragMove();
        }

        // ---- navigation ----
        private void Nav_Click(object s, RoutedEventArgs e)
        {
            if (s is FrameworkElement fe && fe.Tag is string tag) ShowNav(tag);
        }

        private void ShowNav(string tag)
        {
            _lastNav = tag;

            System.Windows.Controls.UserControl target = null;
            if (tag == "drive") target = ViewDrive;
            else if (tag == "proc") target = ViewProc;
            else if (tag == "rules") target = ViewRules;
            else if (tag == "apps") target = ViewApps;
            else if (tag == "voice") target = ViewVoice;
            else if (tag == "set") target = ViewSet;
            if (target == null) return;

            ViewDrive.Visibility = Visibility.Collapsed;
            ViewProc.Visibility = Visibility.Collapsed;
            ViewRules.Visibility = Visibility.Collapsed;
            ViewApps.Visibility = Visibility.Collapsed;
            ViewVoice.Visibility = Visibility.Collapsed;
            ViewSet.Visibility = Visibility.Collapsed;

            // show target with fade + slide
            target.Visibility = Visibility.Visible;
            target.Opacity = 0;
            var tr = new TranslateTransform { X = 20 };
            target.RenderTransform = tr;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.25)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var slideIn = new DoubleAnimation(20, 0, TimeSpan.FromSeconds(0.25)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            target.BeginAnimation(OpacityProperty, fadeIn);
            tr.BeginAnimation(TranslateTransform.XProperty, slideIn);

            HighlightNav(tag);
            if (tag == "proc") ViewProc.UpdateDriveLabel();
            SoundService.Instance.Play(SoundService.Clip.Click);
        }

        private void HighlightNav(string tag)
        {
            foreach (var b in new[] { NavDrive, NavProc, NavRules, NavApps, NavVoice, NavSet })
            {
                bool active = (b.Tag as string) == tag;
                b.Foreground = active ? (System.Windows.Media.Brush)FindResource("CyanGlow") : (System.Windows.Media.Brush)FindResource("TextMuted");
            }
        }

        // ---- dev toggle ----
        private void DevToggle_Click(object s, RoutedEventArgs e)
        {
            _devMode = !_devMode;
            if (_devMode)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.2));
                fadeOut.Completed += (se, ev) =>
                {
                    MainPane.Visibility = Visibility.Collapsed;
                    ViewDev.Visibility = Visibility.Visible;
                    ViewDev.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3)));
                };
                MainPane.BeginAnimation(OpacityProperty, fadeOut);
                ViewDev.Print("[click '← Back to DriveUniverse' to return.]");
                ViewDev.ExitRequested += ReturnFromDev;
            }
            else ReturnFromDev();
        }

        private void ReturnFromDev()
        {
            _devMode = false;
            ViewDev.Visibility = Visibility.Collapsed;
            MainPane.Visibility = Visibility.Visible;
            MainPane.Opacity = 0;
            MainPane.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3)));
        }

        private void ShowCommandHelp()
        {
            var help = new CommandHelp();
            help.Show();
            Ui.Sound(SoundService.Clip.Click);
        }

        // ---- voice ----

        // Voice confirmation state
        private VoiceIntent? _pendingVoiceAction;
        private DispatcherTimer _confirmTimeout;

        private void WireVoice()
        {
            if (!Host.VoiceReady) return;

            // Route ALL voice commands through HandleVoiceCommand.
            Host.Voice.CommandRecognized += c => Dispatcher.Invoke(() =>
            {
                HandleVoiceCommand(c);
            });

            // Wake word glow on BOTH mascots (tray + widget)
            Host.Voice.WakeWordHeard += () => Dispatcher.Invoke(() =>
            {
                Mascot.SetListening(true);
                _orbitWidget?.Mascot.SetListening(true);
            });
            Host.Voice.CommandWindowExpired += () => Dispatcher.Invoke(() =>
            {
                if (LauncherService.Instance.WakeWordEnabled)
                {
                    Mascot.SetListening(false);
                    _orbitWidget?.Mascot.SetListening(false);
                }
            });

            // Auto-start if enabled
            if (LauncherService.Instance.VoiceEnabled && LauncherService.Instance.AlwaysListening)
            {
                Host.Voice.StartAlwaysListening();
                LogService.Log("WireVoice: always-listening started.");
            }
        }

        private void HandleVoiceCommand(VoiceCommand c)
        {
            // Show on BOTH the tray VoiceSheet AND the widget
            VoiceSheet.Show(c);

            // If we have a pending destructive action, check for confirm/decline first
            if (_pendingVoiceAction.HasValue)
            {
                if (c.Intent == VoiceIntent.Confirm)
                {
                    var action = _pendingVoiceAction.Value;
                    _pendingVoiceAction = null;
                    _confirmTimeout?.Stop();
                    _orbitWidget?.ClearPrompt();

                    if (action == VoiceIntent.Eject || action == VoiceIntent.Disconnect)
                    {
                        if (_orbitWidget != null && _orbitWidget.IsVisible)
                            _orbitWidget.PlayMiniEject(() => Dispatcher.Invoke(() => ViewDrive.Eject_Click(null, null)));
                        else
                            ViewDrive.Eject_Click(null, null);
                    }
                    Ui.Sound(SoundService.Clip.Confirm);
                    return;
                }
                else if (c.Intent == VoiceIntent.Decline)
                {
                    _pendingVoiceAction = null;
                    _confirmTimeout?.Stop();
                    _orbitWidget?.ClearPrompt();
                    _orbitWidget?.ShowCommand("Cancelled", false);
                    return;
                }
                // Any other command cancels the pending action
                _pendingVoiceAction = null;
                _confirmTimeout?.Stop();
                _orbitWidget?.ClearPrompt();
            }

            switch (c.Intent)
            {
                case VoiceIntent.Eject:
                case VoiceIntent.Disconnect:
                    _pendingVoiceAction = c.Intent;
                    _orbitWidget?.ShowConfirm("Eject drive? Say yes or no");
                    Ui.Sound(SoundService.Clip.Listen);
                    _confirmTimeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                    _confirmTimeout.Tick += (s2, e2) =>
                    {
                        _pendingVoiceAction = null;
                        _orbitWidget?.ClearPrompt();
                        _orbitWidget?.ShowCommand("Timed out", false);
                        _confirmTimeout.Stop();
                    };
                    _confirmTimeout.Start();
                    break;

                case VoiceIntent.Connect:
                    if (_orbitWidget != null && _orbitWidget.IsVisible)
                        _orbitWidget.PlayMiniConnect(() => Dispatcher.Invoke(() => ViewDrive.Power_Click(null, null)));
                    else
                        ViewDrive.Power_Click(null, null);
                    break;

                case VoiceIntent.OpenApp:
                    {
                        string spokenName = (c.Argument ?? "").ToLowerInvariant().Trim();
                        if (string.IsNullOrEmpty(spokenName)) break;
                        var match = LauncherService.Instance.Apps.FirstOrDefault(a =>
                        {
                            string appName = (a.Name ?? "").ToLowerInvariant();
                            return appName.Contains(spokenName) || spokenName.Contains(appName);
                        });
                        if (match != null)
                        {
                            LauncherService.Instance.Launch(match);
                            _orbitWidget?.ShowCommand("Opening " + match.Name, true);
                        }
                        else
                        {
                            _orbitWidget?.ShowCommand("App not found: " + c.Argument, false);
                        }
                        break;
                    }

                case VoiceIntent.Status:
                    _orbitWidget?.ShowCommand("Checking status", true);
                    break;

                case VoiceIntent.Help:
                    ShowCommandHelp();
                    break;
            }
        }
    }
}
