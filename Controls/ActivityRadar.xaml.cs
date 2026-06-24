using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DriveUniverse.Controls
{
    /// <summary>
    /// A circular radar-style real-time drive activity visualiser.
    /// The sweep line rotates continuously; when read/write activity is detected
    /// on the target drive, a glowing arc grows outward — like a heartbeat on a radar.
    /// </summary>
    public partial class ActivityRadar : UserControl
    {
        private PerformanceCounter _readCounter;
        private PerformanceCounter _writeCounter;
        private DispatcherTimer _timer;
        private bool _initialized;

        public char? DriveLetter { get; set; }

        public ActivityRadar()
        {
            InitializeComponent();
            Loaded += (s, e) => Init();
            Unloaded += (s, e) => Cleanup();
        }

        private void Init()
        {
            // rotate the sweep line forever
            var sweep = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(3.0))
            { RepeatBehavior = RepeatBehavior.Forever };
            SweepRot.BeginAnimation(RotateTransform.AngleProperty, sweep);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (s, e) => Sample();
            _timer.Start();
        }

        private void SetupCounters()
        {
            if (_initialized || DriveLetter == null) return;
            try
            {
                // Find the PhysicalDisk instance that contains our drive letter.
                // Instance names look like "0 C:" or "1 D: E:" (disk number + letters).
                var cat = new System.Diagnostics.PerformanceCounterCategory("PhysicalDisk");
                var instances = cat.GetInstanceNames();
                string targetInstance = null;

                foreach (var inst in instances)
                {
                    // Instance names include drive letters, e.g. "0 C:" or "1 D:"
                    if (inst.IndexOf(DriveLetter.Value + ":", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        targetInstance = inst;
                        break;
                    }
                }

                // Fallback to _Total if we can't find the specific drive
                if (string.IsNullOrEmpty(targetInstance))
                    targetInstance = "_Total";

                _readCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", targetInstance);
                _writeCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", targetInstance);
                _readCounter.NextValue();
                _writeCounter.NextValue();
                _initialized = true;
            }
            catch
            {
                _initialized = false;
            }
        }

        private void Sample()
        {
            if (!_initialized) SetupCounters();
            if (!_initialized) return;

            try
            {
                float read = _readCounter?.NextValue() ?? 0;
                float write = _writeCounter?.NextValue() ?? 0;
                double total = read + write;

                // map bytes/sec to arc angle (0 = idle, 270 = max)
                double normalized = Math.Min(1.0, total / (50 * 1024 * 1024)); // 50MB/s = full
                double angle = normalized * 270;

                if (angle > 5)
                {
                    DrawArc(angle);
                    Label.Text = FormatSpeed(total);
                    Arc.Opacity = 0.7 + normalized * 0.3;
                }
                else
                {
                    Arc.Data = Geometry.Empty;
                    Label.Text = "idle";
                }
            }
            catch { }
        }

        private void DrawArc(double sweepAngle)
        {
            // Draw an arc from the top, sweeping clockwise by sweepAngle degrees
            double startAngle = -90;
            double endAngle = startAngle + sweepAngle;
            double radius = 38;
            double cx = 50, cy = 50;

            double startX = cx + radius * Math.Cos(startAngle * Math.PI / 180);
            double startY = cy + radius * Math.Sin(startAngle * Math.PI / 180);
            double endX = cx + radius * Math.Cos(endAngle * Math.PI / 180);
            double endY = cy + radius * Math.Sin(endAngle * Math.PI / 180);

            bool largeArc = sweepAngle > 180;
            int direction = 1; // clockwise

            var path = string.Format("M {0:F1},{1:F1} A {2:F1},{2:F1} 0 {3} {4} {5:F1},{6:F1}",
                startX, startY, radius, largeArc ? 1 : 0, direction, endX, endY);

            Arc.Data = Geometry.Parse(path);
        }

        private string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec >= 1024 * 1024) return (bytesPerSec / (1024 * 1024)).ToString("F0") + " MB";
            if (bytesPerSec >= 1024) return (bytesPerSec / 1024).ToString("F0") + " KB";
            return bytesPerSec.ToString("F0") + " B";
        }

        private void Cleanup()
        {
            _timer?.Stop();
            _readCounter?.Dispose();
            _writeCounter?.Dispose();
        }
    }
}
