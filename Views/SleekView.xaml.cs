// =====================================================================
//  SleekView.xaml.cs  —  BUILD v8 (compact, quick apps, fixed clicks)
// =====================================================================
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DriveUniverse.Services;

namespace DriveUniverse.Views
{
    public partial class SleekView : UserControl
    {
        public const string BuildTag = "SleekView v8";

        private DriveEntry _selected;
        private ResolvedDevice _resolved;
        // Track the last USB device instance ID so we can re-select it after eject/power-cycle
        private string _lastUsbInstance;

        public SleekView()
        {
            LogService.Log("SleekView: constructor start (" + BuildTag + ")");
            InitializeComponent();
            LogService.Log("SleekView: InitializeComponent done.");

            foreach (var s in new[] { "1", "2", "3" }) CyclesCombo.Items.Add(s);
            foreach (var s in new[] { "0.5s", "1s", "1.5s", "2s", "3s" }) DelayCombo.Items.Add(s);

            int ci = Math.Max(0, Math.Min(2, LauncherService.Instance.CycleCount - 1));
            int di = DelayIndex(LauncherService.Instance.CycleDelayMs);
            CyclesCombo.SelectedIndex = ci;
            DelayCombo.SelectedIndex = di;

            CyclesCombo.SelectionChanged += Settings_Changed;
            DelayCombo.SelectionChanged += Settings_Changed;

            Loaded += (s, e) =>
            {
                RefreshDrives();
                LoadQuickApps();
            };
            LogService.Log("SleekView: constructor done.");
        }

        // ----------------------------------------------------- quick apps strip

        private void LoadQuickApps()
        {
            try { QuickApps.ItemsSource = LauncherService.Instance.Apps; }
            catch (Exception ex) { LogService.Log("QuickApps load error: " + ex.Message); }
        }

        private void QuickApp_Click(object s, RoutedEventArgs e)
        {
            if (s is FrameworkElement fe && fe.Tag is LinkedApp app)
            {
                var res = LauncherService.Instance.Launch(app);
                Ui.Toast(res.Message);
                Ui.Sound(res.Ok ? SoundService.Clip.Click : SoundService.Clip.Error);
            }
        }

        // ----------------------------------------------------- settings

        private int DelayIndex(int ms)
        {
            if (ms <= 500) return 0;
            if (ms <= 1000) return 1;
            if (ms <= 1500) return 2;
            if (ms <= 2000) return 3;
            return 4;
        }

        private int DelayFromIndex(int i)
        {
            int[] v = { 500, 1000, 1500, 2000, 3000 };
            if (i < 0) i = 2;
            if (i >= v.Length) i = v.Length - 1;
            return v[i];
        }

        private int Cycles
        {
            get
            {
                if (CyclesCombo.SelectedIndex < 0) return 1;
                if (int.TryParse(CyclesCombo.SelectedItem as string, out int n)) return n;
                return 1;
            }
        }

        private void Settings_Changed(object s, RoutedEventArgs e)
        {
            if (CyclesCombo.SelectedIndex < 0 || DelayCombo.SelectedIndex < 0) return;
            LauncherService.Instance.CycleCount = Cycles;
            LauncherService.Instance.CycleDelayMs = DelayFromIndex(DelayCombo.SelectedIndex);
            LauncherService.Instance.Save();
        }

        // ----------------------------------------------------- drive list

        public void RefreshDrives()
        {
            // Remember what was selected before refresh
            string lastLetter = _selected != null && !_selected.IsOffOnly ? _selected.Letter.ToString() : null;
            string lastInstance = _selected != null ? _selected.DiskPnpId : _lastUsbInstance;

            DriveCombo.Items.Clear();

            var active = DriveService.GetDrives();
            foreach (var d in active) DriveCombo.Items.Add(d);

            foreach (var dev in DriveService.GetUsbMassStorageDevices())
            {
                if (dev.ErrorCode == 0) continue;
                DriveCombo.Items.Add(new DriveEntry
                {
                    Letter = '\0', DiskPnpId = dev.InstanceId, DiskFriendly = dev.Name, IsOffOnly = true,
                    LastPort = DriveService.ExtractPortLabel(dev.InstanceId)
                });
            }

            // Re-select: try instance ID match first (works after eject), then letter
            int selectIndex = -1;

            // 1. Match by instance ID (the ejected device node survives eject)
            if (!string.IsNullOrEmpty(lastInstance))
            {
                for (int i = 0; i < DriveCombo.Items.Count; i++)
                {
                    if (DriveCombo.Items[i] is DriveEntry de &&
                        string.Equals(de.DiskPnpId, lastInstance, StringComparison.OrdinalIgnoreCase))
                    {
                        selectIndex = i;
                        break;
                    }
                }
            }

            // 2. Match by letter (drive came back online with same letter)
            if (selectIndex < 0 && !string.IsNullOrEmpty(lastLetter))
            {
                for (int i = 0; i < DriveCombo.Items.Count; i++)
                {
                    if (DriveCombo.Items[i] is DriveEntry de && !de.IsOffOnly && de.Letter.ToString() == lastLetter)
                    {
                        selectIndex = i;
                        break;
                    }
                }
            }

            // 3. Prefer USB drives over system drive
            if (selectIndex < 0)
            {
                for (int i = 0; i < DriveCombo.Items.Count; i++)
                {
                    if (DriveCombo.Items[i] is DriveEntry de && de.IsUsb && !de.IsSystem)
                    {
                        selectIndex = i;
                        break;
                    }
                }
            }

            // 4. Last resort: first non-system drive
            if (selectIndex < 0)
            {
                for (int i = 0; i < DriveCombo.Items.Count; i++)
                {
                    if (DriveCombo.Items[i] is DriveEntry de && !de.IsSystem)
                    {
                        selectIndex = i;
                        break;
                    }
                }
            }

            if (selectIndex >= 0) DriveCombo.SelectedIndex = selectIndex;
            else if (DriveCombo.Items.Count > 0) DriveCombo.SelectedIndex = 0;
            else UpdateStatus();
        }

        private static bool Same(DriveEntry a, DriveEntry b) =>
            (!a.IsOffOnly && !b.IsOffOnly && a.Letter == b.Letter) ||
            (a.IsOffOnly && b.IsOffOnly &&
             string.Equals(a.DiskPnpId, b.DiskPnpId, StringComparison.OrdinalIgnoreCase));

        private void DriveCombo_Changed(object s, SelectionChangedEventArgs e)
        {
            if (DriveCombo.SelectedItem is DriveEntry d)
            {
                _selected = d;
                _resolved = null;
                UpdateStatus();
                // Sync the process monitor to the selected drive
                if (!d.IsOffOnly && !d.IsSystem)
                {
                    Host.Monitor.DriveLetter = d.Letter;
                    Activity.DriveLetter = d.Letter;
                }
            }
        }

        private void Refresh_Click(object s, RoutedEventArgs e)
        {
            Ui.Sound(SoundService.Clip.Click);
            RefreshDrives();
        }

        // ----------------------------------------------------- status

        private void UpdateStatus()
        {
            bool off = _selected != null && _selected.IsOffOnly;
            bool sys = _selected != null && _selected.IsSystem;
            BtnEject.IsEnabled = !off && !sys;
            BtnBlockers.IsEnabled = !off && !sys;
            BtnPower.IsEnabled = _selected != null && !sys;
            BtnRoutine.IsEnabled = _selected != null && !sys;

            if (_selected == null)
            {
                StatusName.Text = "No drive";
                StatusDetail.Text = "Plug in or rescan";
                SetOrb(Colors.DimGray);
                return;
            }

            if (sys)
            {
                StatusName.Text = _selected.Letter + ":  (System Drive)";
                StatusDetail.Text = "Contains Windows - cannot be ejected.";
                SetOrb(Colors.DimGray);
                return;
            }

            if (off)
            {
                var st = DriveService.GetStatus(_selected.DiskPnpId);
                StatusName.Text = _selected.DiskFriendly ?? "USB device";
                StatusDetail.Text = st.ProblemText;
                SetOrb(Color.FromRgb(0xFC, 0xB0, 0x5A));
                return;
            }

            StatusName.Text = _selected.Letter + ":" + (string.IsNullOrEmpty(_selected.VolumeLabel) ? "" : "  " + _selected.VolumeLabel);
            StatusDetail.Text = _selected.DiskFriendly + (_selected.IsUsb ? "  -  USB" : "");
            SetOrb(Color.FromRgb(0x52, 0xE6, 0xA0));
        }

        private void SetOrb(Color c)
        {
            OrbCore.Color = c;
            var pulse = new DoubleAnimation(0.8, 1.0, TimeSpan.FromSeconds(1.6))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            StatusOrb.BeginAnimation(OpacityProperty, pulse);
        }

        // ----------------------------------------------------- resolve

        private ResolvedDevice ResolveSelected()
        {
            if (_selected == null) return null;
            if (_selected.IsOffOnly) return null;
            if (_resolved == null || _resolved.Letter != _selected.Letter || !_resolved.IsValid)
                _resolved = DriveService.Resolve(_selected.Letter);
            return _resolved.IsValid ? _resolved : null;
        }

        // ----------------------------------------------------- actions

        private void Blockers_Click(object s, RoutedEventArgs e)
        {
            Ui.Sound(SoundService.Clip.Click);
            if (_selected == null || _selected.IsOffOnly) { Ui.Toast("Pick an active drive to scan."); return; }
            Task.Run(() =>
            {
                var lockers = DriveService.FindLockers(_selected.Letter);
                Dispatcher.Invoke(() =>
                {
                    if (lockers.Count == 0) { Ui.Toast("Drive is clear — safe to eject."); Ui.MascotHappy(); }
                    else
                    {
                        Ui.Toast(lockers.Count + " process(es) holding it open — see Processes.");
                        Ui.MascotUhOh();
                        Host.Monitor.DriveLetter = _selected.Letter;
                    }
                    foreach (var l in lockers) Ui.Log("  • " + l.ToString() + (string.IsNullOrEmpty(l.ExecutablePath) ? "" : "   " + l.ExecutablePath));
                });
            });
        }

        public void Eject_Click(object s, RoutedEventArgs e)
        {
            var dev = ResolveSelected();
            if (dev == null) { Ui.Toast("Select an active drive first."); Ui.Sound(SoundService.Clip.Error); return; }
            Host.Monitor.DriveLetter = dev.Letter;
            // Track the USB device instance so we can re-select it after eject
            _lastUsbInstance = dev.UsbMsInstance;
            Ui.Sound(SoundService.Clip.Click);

            // warp on eject: the drive recedes into the void
            Ui.WarpEject("EJECTING", () =>
            {
                var res = DriveService.Eject(dev.EjectDevNode);
                Dispatcher.Invoke(() =>
                {
                    Ui.Log(res.Message);
                    if (res.Success) { Ui.Toast("Ejected."); Ui.MascotHappy(); Ui.Sound(SoundService.Clip.Disconnect); Ui.ResultCallback?.Invoke("Ejected", true); }
                    else { Ui.Toast("Couldn't eject — see Processes."); Ui.MascotUhOh(); Ui.Sound(SoundService.Clip.Error); Ui.ResultCallback?.Invoke("Eject failed", false); }
                    RefreshDrives();
                });
            });
        }

        public void Power_Click(object s, RoutedEventArgs e)
        {
            if (_selected == null) return;
            string target = _selected.IsOffOnly ? _selected.DiskPnpId : ResolveSelected()?.UsbMsInstance;
            if (string.IsNullOrEmpty(target)) { Ui.Toast("Select a device first."); return; }

            int cycles = Cycles, delay = DelayFromIndex(DelayCombo.SelectedIndex);
            Ui.WarpConnect("ENGAGING DRIVE", () =>
            {
                DriveService.PowerCycle(null, target, cycles, delay, m => Ui.Log(m), out bool cameBack);
                Dispatcher.Invoke(() =>
                {
                    if (cameBack) { Ui.Toast("Drive back online."); Ui.MascotHappy(); Ui.Sound(SoundService.Clip.Confirm); Ui.ResultCallback?.Invoke("Online", true); }
                    else { Ui.Toast("Still flagged — try once more."); Ui.MascotUhOh(); Ui.Sound(SoundService.Clip.Error); Ui.ResultCallback?.Invoke("Failed", false); }
                    RefreshDrives();
                });
            });
        }

        private void Routine_Click(object s, RoutedEventArgs e)
        {
            var dev = ResolveSelected();
            if (dev == null) { Ui.Toast("Select an active drive first."); return; }
            int cycles = Cycles, delay = DelayFromIndex(DelayCombo.SelectedIndex);
            Host.Monitor.DriveLetter = dev.Letter;

            Task.Run(() =>
            {
                Ui.Log("==== FULL ROUTINE ====");
                var lockers = DriveService.FindLockers(dev.Letter);
                if (lockers.Count > 0)
                {
                    Dispatcher.Invoke(() => { Ui.Toast("Drive busy — see Processes."); Ui.MascotUhOh(); });
                    foreach (var l in lockers) Ui.Log("  • " + l.ToString());
                    return;
                }
                var ej = DriveService.Eject(dev.EjectDevNode);
                Ui.Log(ej.Message);
                if (!ej.Success) { Dispatcher.Invoke(() => Ui.Toast("Eject blocked.")); return; }

                Dispatcher.Invoke(() => Ui.WarpConnect("ENGAGING DRIVE", () =>
                {
                    DriveService.PowerCycle(dev, dev.UsbMsInstance, cycles, delay, m => Ui.Log(m), out bool cameBack);
                    Dispatcher.Invoke(() =>
                    {
                        if (cameBack) { Ui.Toast("Drive restored."); Ui.MascotHappy(); Ui.Sound(SoundService.Clip.Confirm); }
                        else { Ui.Toast("Try one more cycle."); Ui.MascotUhOh(); }
                        RefreshDrives();
                    });
                }));
            });
        }
    }
}
