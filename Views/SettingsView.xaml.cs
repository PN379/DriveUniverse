using System;
using System.Windows;
using System.Windows.Controls;
using DriveUniverse.Services;

namespace DriveUniverse.Views
{
    /// <summary>
    /// Preferences: sound toggle, warp toggle, default cycle count/delay, and a
    /// shortcut into the raw developer console (the engine-facing toggle).
    /// </summary>
    public partial class SettingsView : UserControl
    {
        public static bool WarpEnabled = true;
        public event System.Action OpenDevConsole;

        private static readonly int[] DelayValues = { 500, 1000, 1500, 2000, 3000 };

        public SettingsView()
        {
            InitializeComponent();
            Loaded += (s, e) => LateInit();
        }

        private void LateInit()
        {
            foreach (var st in new[] { "1", "2", "3", "4" }) CmbCycles.Items.Add(st);
            foreach (var st in new[] { "0.5s", "1s", "1.5s", "2s", "3s" }) CmbDelay.Items.Add(st);

            int ci = LauncherService.Instance.CycleCount - 1;
            CmbCycles.SelectedIndex = ci < 0 ? 0 : ci;

            int ms = LauncherService.Instance.CycleDelayMs;
            int di = 2;
            for (int i = 0; i < DelayValues.Length; i++) if (ms == DelayValues[i]) { di = i; break; }
            CmbDelay.SelectedIndex = di;

            ChkSound.IsChecked = LauncherService.Instance.SoundsEnabled;
            ChkWidgetQuick.IsChecked = LauncherService.Instance.ShowWidgetQuickActions;
            ChkAutoStart.IsChecked = LauncherService.Instance.AutoStart;

            CmbCycles.SelectionChanged += Defaults_Changed;
            CmbDelay.SelectionChanged += Defaults_Changed;
            ChkSound.Checked += Sound_Changed;
            ChkSound.Unchecked += Sound_Changed;
            ChkWarp.Checked += Warp_Changed;
            ChkWarp.Unchecked += Warp_Changed;
            ChkWidgetQuick.Checked += WidgetQuick_Changed;
            ChkWidgetQuick.Unchecked += WidgetQuick_Changed;
            ChkAutoStart.Checked += AutoStart_Changed;
            ChkAutoStart.Unchecked += AutoStart_Changed;
        }

        private void AutoStart_Changed(object s, RoutedEventArgs e)
        {
            bool on = ChkAutoStart.IsChecked == true;
            LauncherService.Instance.AutoStart = on;
            LauncherService.Instance.Save();
            try
            {
                string key = @"Software\Microsoft\Windows\CurrentVersion\Run";
                using (var rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(key, true))
                {
                    if (rk != null)
                    {
                        if (on)
                        {
                            string exe = System.Windows.Application.ResourceAssembly.Location;
                            rk.SetValue("DriveUniverse", "\"" + exe + "\"");
                            Ui.Toast("Will start with Windows");
                        }
                        else
                        {
                            rk.DeleteValue("DriveUniverse", false);
                            Ui.Toast("Won't start with Windows");
                        }
                    }
                }
            }
            catch (Exception ex) { Ui.Toast("Auto-start error: " + ex.Message); }
        }


        private void WidgetQuick_Changed(object s, RoutedEventArgs e)
        {
            bool on = ChkWidgetQuick.IsChecked == true;
            LauncherService.Instance.ShowWidgetQuickActions = on;
            LauncherService.Instance.Save();
            Ui.Toast(on ? "Widget quick actions on" : "Widget quick actions off");
        }

        private void Sound_Changed(object s, RoutedEventArgs e)
        {
            bool on = ChkSound.IsChecked == true;
            LauncherService.Instance.SoundsEnabled = on;
            SoundService.Instance.Enabled = on;
            LauncherService.Instance.Save();
        }

        private void Warp_Changed(object s, RoutedEventArgs e) => WarpEnabled = ChkWarp.IsChecked == true;

        private void Defaults_Changed(object s, RoutedEventArgs e)
        {
            if (CmbCycles.SelectedIndex < 0 || CmbDelay.SelectedIndex < 0) return;
            LauncherService.Instance.CycleCount = int.Parse((string)CmbCycles.SelectedItem);
            int di = CmbDelay.SelectedIndex;
            if (di >= 0 && di < DelayValues.Length)
                LauncherService.Instance.CycleDelayMs = DelayValues[di];
            LauncherService.Instance.Save();
            Ui.Toast("Saved");
        }

        private void Dev_Click(object s, RoutedEventArgs e) => OpenDevConsole?.Invoke();

        private void Support_Click(object s, RoutedEventArgs e)
        {
            TippingService.OpenTipPage();
            Ui.Toast("Thank you!");
        }
    }
}
