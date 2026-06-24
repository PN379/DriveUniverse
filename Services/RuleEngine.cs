using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DriveUniverse.Services
{
    /// <summary>
    /// Evaluates automation rules on a background tick:
    ///  - if the drive has been idle (no process holding it) for >= IdleMinutes,
    ///    fire the rule's action (eject / power-cycle / disconnect).
    ///  - if AutoQuitKnown is set, terminate recurring known processes first.
    /// Reports every action through events the UI / sound service can react to.
    /// </summary>
    public sealed class RuleEngine
    {
        private readonly ProcessMonitor _monitor;
        private CancellationTokenSource _cts;
        public bool Running { get; private set; }

        private DateTime _lastBusy = DateTime.Now;

        public char? DriveLetter
        {
            get => _monitor.DriveLetter;
            set => _monitor.DriveLetter = value;
        }

        public event Action<string> Log;                 // human-readable log line
        public event Action<string> ActionFired;         // rule fired (for sound + toast)

        public RuleEngine(ProcessMonitor monitor) { _monitor = monitor; }

        public void Start()
        {
            if (Running) return;
            Running = true;
            _monitor.Start();
            _cts = new CancellationTokenSource();
            var t = new Thread(() => Loop(_cts.Token)) { IsBackground = true };
            t.Start();
        }

        public void Stop() { Running = false; _cts?.Cancel(); _monitor.Stop(); }

        private async void Loop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, ct);
                    Evaluate();
                }
                catch (TaskCanceledException) { break; }
                catch { }
            }
        }

        private void Evaluate()
        {
            var rules = LauncherService.Instance.Rules.Where(r => r.Enabled).ToList();
            if (rules.Count == 0) { _lastBusy = DateTime.Now; return; }

            var busy = _monitor.Current.Count > 0;
            if (busy) _lastBusy = DateTime.Now;

            var idleMinutes = (DateTime.Now - _lastBusy).TotalMinutes;

            foreach (var r in rules)
            {
                if (idleMinutes < r.IdleMinutes) continue;
                if (!(r.DriveHint == "*" || (DriveLetter.HasValue && DriveHintMatches(r.DriveHint, DriveLetter.Value))))
                    continue;

                Log?.Invoke($"Rule '{r.Name}': drive idle {idleMinutes:0.0}m >= {r.IdleMinutes}m -> firing.");
                if (r.AutoQuitKnown)
                {
                    int n = _monitor.TerminateRecurring();
                    if (n > 0) Log?.Invoke($"   auto-quit {n} recurring process(es).");
                }

                ExecuteRule(r);
                ActionFired?.Invoke(r.Name);
                _lastBusy = DateTime.Now;   // reset so it doesn't refire every tick
            }
        }

        private bool DriveHintMatches(string hint, char letter) =>
            hint.TrimEnd(':').Equals(letter.ToString(), StringComparison.OrdinalIgnoreCase);

        private void ExecuteRule(AutoRule r)
        {
            if (DriveLetter == null) return;
            switch (r.Action)
            {
                case RuleAction.EjectOnly:
                    {
                        var dev = DriveService.Resolve(DriveLetter.Value);
                        if (!dev.IsValid) return;
                        var ej = DriveService.Eject(dev.EjectDevNode);
                        Log?.Invoke("   " + ej.Message);
                        break;
                    }
                case RuleAction.PowerCycle:
                    {
                        var dev = DriveService.Resolve(DriveLetter.Value);
                        if (!dev.IsValid) return;
                        DriveService.PowerCycle(dev, dev.UsbMsInstance,
                            LauncherService.Instance.CycleCount,
                            LauncherService.Instance.CycleDelayMs,
                            m => Log?.Invoke("   " + m), out _);
                        break;
                    }
                case RuleAction.DisconnectUsb:
                    {
                        var dev = DriveService.Resolve(DriveLetter.Value);
                        if (dev.IsValid) Win32.CM_Disable_DevNode(dev.UsbMsDevNode, Win32.DISABLE_FORCE);
                        Log?.Invoke("   USB device disabled.");
                        break;
                    }
            }
        }
    }
}
