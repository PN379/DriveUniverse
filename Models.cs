using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DriveUniverse
{
    // ---- Settings: linked applications (e.g. Defraggler) ----------------------
    public class LinkedApp : INotifyPropertyChanged
    {
        private string _name, _path, _args;
        private System.Windows.Media.ImageSource _icon;
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Path { get => _path; set { _path = value; OnPropertyChanged(); } }
        public string Args { get => _args; set { _args = value; OnPropertyChanged(); } }
        public string Glyph { get; set; } = "◆";
        [System.Xml.Serialization.XmlIgnore]
        public System.Windows.Media.ImageSource AppIcon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string p = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    // ---- Settings: automation rules ------------------------------------------
    public class AutoRule : INotifyPropertyChanged
    {
        private bool _enabled;
        private int _idleMinutes;
        private bool _autoQuitKnown;
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
        public string Name { get; set; }
        public string DriveHint { get; set; }            // letter or "*"
        public RuleAction Action { get; set; }
        public int IdleMinutes { get => _idleMinutes; set { _idleMinutes = value; OnPropertyChanged(); } }
        public bool Enabled { get => _enabled; set { _enabled = value; OnPropertyChanged(); } }
        public bool AutoQuitKnown { get => _autoQuitKnown; set { _autoQuitKnown = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string p = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public enum RuleAction { EjectOnly, PowerCycle, DisconnectUsb }

    // ---- Runtime: a process currently holding the drive -----------------------
    public class DriveProcess : INotifyPropertyChanged
    {
        private bool _recurring;
        public int Pid { get; set; }
        public string AppName { get; set; }
        public string ExePath { get; set; }
        public string TypeName { get; set; }
        public bool Recurring { get => _recurring; set { _recurring = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string p = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
