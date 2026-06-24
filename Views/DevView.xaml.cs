using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DriveUniverse.Services;

namespace DriveUniverse.Views
{
    /// <summary>
    /// Raw developer console: shows every engine operation and lets you run
    /// text commands (list, eject <letter>, cycle <letter>, blockers <letter>,
    /// status, help). This is the "dev" half of the dev/sleek toggle.
    /// </summary>
    public partial class DevView : UserControl
    {
        public DevView()
        {
            InitializeComponent();
            Print("DriveUniverse dev console — engine online.");
            Print("Type 'help' for commands.");
            Print("");
        }

        public event System.Action ExitRequested;

        private void Exit_Click(object sender, RoutedEventArgs e) => ExitRequested?.Invoke();

        public void Print(string line)
        {
            Output.AppendText(line + Environment.NewLine);
            Scroller.ScrollToBottom();
        }

        private void Cmd_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            string input = Cmd.Text.Trim();
            Cmd.Clear();
            Print("> " + input);
            Run(input);
        }

        private void Run(string input)
        {
            try
            {
                string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return;
                string cmd = parts[0].ToLowerInvariant();

                switch (cmd)
                {
                    case "help":
                        Print("Commands:");
                        Print("  list            list drives + devices");
                        Print("  status          show USB mass-storage device codes");
                        Print("  eject <X>       eject drive X:");
                        Print("  cycle <X>       power-cycle (force disable x2 -> enable)");
                        Print("  blockers <X>    who holds drive X: open");
                        Print("  apps            list linked apps");
                        Print("  clear           clear output");
                        break;
                    case "list":
                        foreach (var d in DriveService.GetDrives())
                            Print(string.Format("  {0}: {1} {2}", d.Letter, d.DiskFriendly, d.IsUsb ? "[USB]" : ""));
                        foreach (var v in DriveService.GetUsbMassStorageDevices())
                            Print(string.Format("  dev {0} -> code {1}: {2}", v.Name, v.ErrorCode, v.Problem));
                        break;
                    case "status":
                        foreach (var v in DriveService.GetUsbMassStorageDevices())
                            Print(string.Format("  {0} | {1} | {2}", v.Name, v.Status, v.Problem));
                        break;
                    case "eject":
                        if (parts.Length > 1 && char.IsLetter(parts[1][0]))
                        {
                            var dev = DriveService.Resolve(parts[1][0]);
                            if (!dev.IsValid) { Print(dev.Error); break; }
                            var r = DriveService.Eject(dev.EjectDevNode);
                            Print(r.Message);
                        }
                        else Print("usage: eject <X>");
                        break;
                    case "cycle":
                        if (parts.Length > 1 && char.IsLetter(parts[1][0]))
                        {
                            var dev = DriveService.Resolve(parts[1][0]);
                            if (!dev.IsValid) { Print(dev.Error); break; }
                            DriveService.PowerCycle(dev, dev.UsbMsInstance,
                                LauncherService.Instance.CycleCount,
                                LauncherService.Instance.CycleDelayMs,
                                m => Print("  " + m), out bool back);
                            Print(back ? "  -> drive back online" : "  -> still flagged");
                        }
                        else Print("usage: cycle <X>");
                        break;
                    case "blockers":
                        if (parts.Length > 1 && char.IsLetter(parts[1][0]))
                        {
                            foreach (var l in DriveService.FindLockers(parts[1][0]))
                                Print(string.Format("  {0} ({1}) {2}", l.AppName, l.Pid, l.ExecutablePath ?? ""));
                        }
                        else Print("usage: blockers <X>");
                        break;
                    case "apps":
                        foreach (var a in LauncherService.Instance.Apps)
                            Print(string.Format("  {0} -> {1}", a.Name, a.Path));
                        break;
                    case "clear":
                        Output.Clear();
                        break;
                    default:
                        Print("unknown command: " + cmd + " (try 'help')");
                        break;
                }
            }
            catch (Exception ex) { Print("error: " + ex.Message); }
        }
    }
}
