using System;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace DriveUniverse.Controls
{
    /// <summary>
    /// Glowing "DriveUniverse" title. The gradient slowly shifts (like a nebula
    /// drifting) and the glow breathes — feels alive and spacey.
    /// </summary>
    public partial class LogoTitle : UserControl
    {
        public LogoTitle()
        {
            InitializeComponent();
            Loaded += (s, e) => Animate();
        }

        private void Animate()
        {
            // gradient shift — the three color stops slowly move offset positions
            var gs1a = new DoubleAnimation(0.0, 0.3, TimeSpan.FromSeconds(8.0))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            var gs2a = new DoubleAnimation(0.6, 0.4, TimeSpan.FromSeconds(8.0))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            GS1.BeginAnimation(GradientStop.OffsetProperty, gs1a);
            GS2.BeginAnimation(GradientStop.OffsetProperty, gs2a);

            // glow breathe
            var glow = new DoubleAnimation(0.4, 0.9, TimeSpan.FromSeconds(3.5))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            Glow.BeginAnimation(DropShadowEffect.OpacityProperty, glow);

            // glow color cycles between cyan and purple
            var glowColor = new ColorAnimation(
                Color.FromRgb(0x45, 0xE7, 0xF7),
                Color.FromRgb(0x8E, 0x70, 0xFF),
                TimeSpan.FromSeconds(7.0))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
            Glow.BeginAnimation(DropShadowEffect.ColorProperty, glowColor);
        }
    }
}
