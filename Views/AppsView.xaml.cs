using System.Windows;
using System.Windows.Controls;
using DriveUniverse.Services;
using Microsoft.Win32;

namespace DriveUniverse.Views
{
    /// <summary>
    /// Manage and launch linked applications (Defraggler, Disk Management, etc.).
    /// Apps are persisted and also registered as voice targets ("open Defraggler").
    /// </summary>
    public partial class AppsView : UserControl
    {
        public AppsView()
        {
            InitializeComponent();
            List.DataContext = LauncherService.Instance.Apps;
            LauncherService.Instance.Apps.CollectionChanged += (s, e) => UpdateEmpty();
            Loaded += (s, e) => LoadIconsAndRefresh();
        }

        private void LoadIconsAndRefresh()
        {
            foreach (var app in LauncherService.Instance.Apps)
            {
                if (app.AppIcon == null)
                {
                    try { app.AppIcon = IconExtractor.Extract(app.Path); } catch { }
                }
            }
            UpdateEmpty();
        }

        private void UpdateEmpty()
        {
            Dispatcher.Invoke(() =>
            {
                EmptyApps.Visibility = LauncherService.Instance.Apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void Add_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Choose an application",
                Filter = "Applications (*.exe;*.msc;*.bat)|*.exe;*.msc;*.bat|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            var name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);
            var app = new LinkedApp
            {
                Name = name,
                Glyph = GlyphFor(name),
                Path = dlg.FileName
            };
            try { app.AppIcon = IconExtractor.Extract(dlg.FileName); } catch { }
            LauncherService.Instance.Apps.Add(app);
            LauncherService.Instance.Save();
            Host.Voice?.RegisterApp(name);
            Host.Voice?.ReloadGrammar();
            Ui.Toast("Added " + name);
            Ui.Sound(SoundService.Clip.Click);
        }

        private void Launch_Click(object s, RoutedEventArgs e)
        {
            if (((FrameworkElement)s).Tag is LinkedApp app)
            {
                var res = LauncherService.Instance.Launch(app);
                Ui.Toast(res.Message);
                Ui.Sound(res.Ok ? SoundService.Clip.Click : SoundService.Clip.Error);
            }
        }

        private void Remove_Click(object s, RoutedEventArgs e)
        {
            if (((FrameworkElement)s).Tag is LinkedApp app)
            {
                LauncherService.Instance.Apps.Remove(app);
                LauncherService.Instance.Save();
                Ui.Toast("Removed " + app.Name);
            }
        }

        private string GlyphFor(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("defrag")) return "▤";
            if (n.Contains("disk") || n.Contains("manage")) return "▣";
            if (n.Contains("device")) return "⚙";
            if (n.Contains("backup")) return "☬";
            if (n.Contains("format")) return "▦";
            return "◆";
        }
    }
}
