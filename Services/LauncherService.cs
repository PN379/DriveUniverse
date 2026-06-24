using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Xml.Serialization;

namespace DriveUniverse.Services
{
    /// <summary>
    /// Manages linked applications (Defraggler, CrystalDiskInfo, backups, …) and
    /// the automation rules, persisted to %AppData%\DriveUniverse\settings.xml.
    /// </summary>
    public sealed class LauncherService
    {
        private static LauncherService _instance;
        public static LauncherService Instance => _instance ?? (_instance = new LauncherService());

        public ObservableCollection<LinkedApp> Apps { get; } = new ObservableCollection<LinkedApp>();
        public ObservableCollection<AutoRule> Rules { get; } = new ObservableCollection<AutoRule>();

        private static string SettingsDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DriveUniverse");
        private static string SettingsFile => Path.Combine(SettingsDir, "settings.xml");

        private LauncherService()
        {
            Defaults();
            Load();
            if (Apps.Count == 0) SeedApps();
            if (Rules.Count == 0) SeedRules();
        }

        private void Defaults()
        {
            VoiceEnabled = false;
            AlwaysListening = false;
            SoundsEnabled = true;
            CycleCount = 2;
            CycleDelayMs = 1500;
        }

        // persisted user prefs
        public bool VoiceEnabled { get; set; }
        public bool AlwaysListening { get; set; }
        public bool WakeWordEnabled { get; set; } = true;
        public bool AutoStart { get; set; } = false;
        public bool SoundsEnabled { get; set; }
        public int CycleCount { get; set; }
        public int CycleDelayMs { get; set; }
        public int SettingsVersion { get; set; } = 2;
        public bool ShowWidgetQuickActions { get; set; } = true;

        private void SeedApps()
        {
            // Start empty — the user adds their own apps via the Apps tab.
        }

        private void SeedRules()
        {
            Rules.Add(new AutoRule
            {
                Name = "Idle auto-off",
                DriveHint = "*",
                Action = RuleAction.EjectOnly,
                IdleMinutes = 15,
                Enabled = false,
                AutoQuitKnown = false
            });
        }

        public LaunchResult Launch(LinkedApp app)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(app?.Path))
                    return new LaunchResult { Ok = false, Message = "No path set." };

                var psi = new ProcessStartInfo
                {
                    FileName = app.Path,
                    UseShellExecute = true
                };
                if (!string.IsNullOrWhiteSpace(app.Args)) psi.Arguments = app.Args;

                Process.Start(psi);
                return new LaunchResult { Ok = true, Message = "Launched " + app.Name };
            }
            catch (Exception ex)
            {
                return new LaunchResult { Ok = false, Message = ex.Message };
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var dto = new SettingsDto
                {
                    VoiceEnabled = VoiceEnabled,
                    AlwaysListening = AlwaysListening,
                    WakeWordEnabled = WakeWordEnabled,
                    SoundsEnabled = SoundsEnabled,
                    CycleCount = CycleCount,
                    CycleDelayMs = CycleDelayMs,
                    SettingsVersion = SettingsVersion,
                    ShowWidgetQuickActions = ShowWidgetQuickActions,
                    AutoStart = AutoStart,
                    Apps = new System.Collections.Generic.List<LinkedApp>(Apps),
                    Rules = new System.Collections.Generic.List<AutoRule>(Rules)
                };
                var xs = new XmlSerializer(typeof(SettingsDto));
                using (var w = new StreamWriter(SettingsFile, false, Encoding.UTF8))
                    xs.Serialize(w, dto);
            }
            catch { /* settings are best-effort */ }
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return;
                var xs = new XmlSerializer(typeof(SettingsDto));
                using (var r = new StreamReader(SettingsFile, Encoding.UTF8))
                {
                    var dto = (SettingsDto)xs.Deserialize(r);
                    VoiceEnabled = dto.VoiceEnabled;
                    AlwaysListening = dto.AlwaysListening;
                    WakeWordEnabled = dto.WakeWordEnabled;
                    AutoStart = dto.AutoStart;
                    SoundsEnabled = dto.SoundsEnabled;
                    // Migration: if the saved settings are from an older version,
                    // apply the new defaults (don't let old CycleCount=1 override).
                    if (dto.SettingsVersion < 2)
                    {
                        CycleCount = 2;
                        CycleDelayMs = 1500;
                        SettingsVersion = 2;
                        Save();
                    }
                    else
                    {
                        CycleCount = dto.CycleCount;
                        CycleDelayMs = dto.CycleDelayMs;
                        SettingsVersion = dto.SettingsVersion;
                        ShowWidgetQuickActions = dto.ShowWidgetQuickActions;
                    }
                    Apps.Clear();
                    if (dto.Apps != null) foreach (var a in dto.Apps) Apps.Add(a);
                    Rules.Clear();
                    if (dto.Rules != null) foreach (var rule in dto.Rules) Rules.Add(rule);
                }
            }
            catch { }
        }
    }

    public class LaunchResult { public bool Ok; public string Message; }

    [Serializable]
    [XmlRoot("DriveUniverseSettings")]
    public class SettingsDto
    {
        public bool VoiceEnabled { get; set; }
        public bool AlwaysListening { get; set; }
        public bool SoundsEnabled { get; set; }
        public bool WakeWordEnabled { get; set; } = true;
        public int CycleCount { get; set; }
        public int CycleDelayMs { get; set; }
        public int SettingsVersion { get; set; }
        public bool ShowWidgetQuickActions { get; set; }
        public bool AutoStart { get; set; }
        [XmlArray("Apps")] [XmlArrayItem("App")] public System.Collections.Generic.List<LinkedApp> Apps { get; set; }
        [XmlArray("Rules")] [XmlArrayItem("Rule")] public System.Collections.Generic.List<AutoRule> Rules { get; set; }
    }
}
