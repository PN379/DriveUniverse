using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DriveUniverse.Services
{
    /// <summary>
    /// Periodically scans a target drive for processes holding it open (via the
    /// Restart Manager, same as "Why can't it eject?"). Tracks which offenders
    /// recur so the UI can flag them and (if a rule allows) auto-quit them.
    /// </summary>
    public sealed class ProcessMonitor
    {
        public ObservableCollection<DriveProcess> Current { get; } = new ObservableCollection<DriveProcess>();

        // exe path -> how many consecutive scans it appeared
        private readonly Dictionary<string, int> _history = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _known = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // whitelisted recurring

        public char? DriveLetter { get; set; }
        private CancellationTokenSource _cts;
        public bool Running { get; private set; }

        public event Action ScanCompleted;

        public void Start(int intervalMs = 4000)
        {
            if (Running) return;
            Running = true;
            _cts = new CancellationTokenSource();
            var t = new Thread(() => Loop(intervalMs, _cts.Token)) { IsBackground = true };
            t.Start();
        }

        public void Stop()
        {
            Running = false;
            _cts?.Cancel();
        }

        private void Loop(int intervalMs, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                ScanOnce();
                try { Task.Delay(intervalMs, ct).Wait(); } catch { break; }
            }
        }

        /// <summary>One-shot scan. Safe to call from the UI thread or background.</summary>
        public List<DriveProcess> ScanOnce()
        {
            var list = new List<DriveProcess>();
            if (DriveLetter == null) return list;
            try
            {
                var lockers = DriveService.FindLockers(DriveLetter.Value);
                foreach (var l in lockers)
                {
                    var key = !string.IsNullOrEmpty(l.ExecutablePath) ? l.ExecutablePath : l.AppName;
                    int c;
                    _history.TryGetValue(key, out c);
                    if (lockers.Count > 0 || true) c = c + 1;  // count presence this scan
                    _history[key] = c;

                    list.Add(new DriveProcess
                    {
                        Pid = (int)l.Pid,
                        AppName = l.AppName,
                        ExePath = l.ExecutablePath,
                        TypeName = l.TypeName,
                        Recurring = c >= 3 || _known.Contains(key)
                    });
                }
                // decay absent processes so a one-off doesn't stay "recurring"
                var seen = new HashSet<string>(list.Select(x => !string.IsNullOrEmpty(x.ExePath) ? x.ExePath : x.AppName), StringComparer.OrdinalIgnoreCase);
                var decay = _history.Keys.Where(k => !seen.Contains(k)).ToList();
                foreach (var k in decay) _history[k] = Math.Max(0, _history[k] - 2);
            }
            catch { /* drive not mounted etc. */ }
            finally
            {
                PushToCollection(list);
                ScanCompleted?.Invoke();
            }
            return list;
        }

        private void PushToCollection(List<DriveProcess> list)
        {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    Current.Clear();
                    foreach (var p in list) Current.Add(p);
                });
        }

        public void MarkKnown(string exePath)
        {
            if (!string.IsNullOrEmpty(exePath)) _known.Add(exePath);
        }

        public bool Terminate(DriveProcess p)
        {
            try
            {
                var proc = Process.GetProcessById(p.Pid);
                proc.CloseMainWindow();
                if (!proc.WaitForExit(2500)) proc.Kill();
                return true;
            }
            catch { return false; }
        }

        /// <summary>Auto-quit every process flagged recurring (used by rules).</summary>
        public int TerminateRecurring()
        {
            int n = 0;
            foreach (var p in Current.ToList())
                if (p.Recurring && Terminate(p)) n++;
            return n;
        }

        /// <summary>True when only whitelisted/familiar processes (or nothing) hold the drive.</summary>
        public bool OnlyFamiliarActive()
        {
            if (Current.Count == 0) return true;
            return Current.All(p => _known.Contains(p.ExePath ?? p.AppName));
        }
    }
}
