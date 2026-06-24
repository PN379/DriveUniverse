using System.Windows;
using System.Windows.Controls;
using DriveUniverse.Services;

namespace DriveUniverse.Views
{
    /// <summary>
    /// "Rule block" editor: define timers that auto-eject / power-cycle / disconnect
    /// a drive after it has been idle (no process holding it) for N minutes, with
    /// optional auto-quit of recurring known processes first. Rules are live-built
    /// into cards so the user can toggle each one independently.
    /// </summary>
    public partial class RulesView : UserControl
    {
        public RulesView()
        {
            InitializeComponent();
            BuildCards();
        }

        private void BuildCards()
        {
            Stack.Children.Clear();
            foreach (var r in LauncherService.Instance.Rules)
                Stack.Children.Add(MakeCard(r));

            var add = new Button
            {
                Style = (Style)FindResource("GhostButton"),
                Content = "+  New rule",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0)
            };
            add.Click += (s, e) =>
            {
                var nr = new AutoRule { Name = "New rule", DriveHint = "*", Action = RuleAction.EjectOnly, IdleMinutes = 10, Enabled = true };
                LauncherService.Instance.Rules.Add(nr);
                LauncherService.Instance.Save();
                Stack.Children.Insert(Stack.Children.Count - 1, MakeCard(nr));
                Ui.Toast("Rule added");
            };
            Stack.Children.Add(add);
        }

        private UIElement MakeCard(AutoRule r)
        {
            var card = new Border
            {
                Margin = new Thickness(0, 0, 0, 10),
                Padding = new Thickness(16, 12, 16, 12),
                CornerRadius = new CornerRadius(12),
                Background = (System.Windows.Media.Brush)FindResource("PanelHiBrush")
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // left column: name + config
            var left = new StackPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            var toggle = new CheckBox
            {
                Content = r.Name,
                IsChecked = r.Enabled,
                Foreground = (System.Windows.Media.Brush)FindResource("TextBright"),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            toggle.Checked += (s, e) => { r.Enabled = true; LauncherService.Instance.Save(); };
            toggle.Unchecked += (s, e) => { r.Enabled = false; LauncherService.Instance.Save(); };
            header.Children.Add(toggle);
            left.Children.Add(header);

            // config row
            var cfg = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(28, 6, 0, 0) };

            cfg.Children.Add(MkLabel("After"));
            var mins = new TextBox { Text = r.IdleMinutes.ToString(), Width = 40, Margin = new Thickness(6, 0, 6, 0), Padding = new Thickness(4, 2, 4, 2) };
            mins.TextChanged += (s, e) => { if (int.TryParse(mins.Text, out int v)) { r.IdleMinutes = v; LauncherService.Instance.Save(); } };
            cfg.Children.Add(mins);
            cfg.Children.Add(MkLabel("min idle,"));

            cfg.Children.Add(MkLabel("  do"));
            var action = new ComboBox { Width = 150, Margin = new Thickness(6, 0, 6, 0) };
            action.Items.Add("Eject only"); action.Items.Add("Power-cycle (wake)"); action.Items.Add("Disconnect USB");
            action.SelectedIndex = (int)r.Action;
            action.SelectionChanged += (s, e) => { if (action.SelectedIndex >= 0) { r.Action = (RuleAction)action.SelectedIndex; LauncherService.Instance.Save(); } };
            cfg.Children.Add(action);

            var autoQuit = new CheckBox
            {
                Content = "auto-quit recurring",
                IsChecked = r.AutoQuitKnown,
                Foreground = (System.Windows.Media.Brush)FindResource("TextMuted"),
                FontSize = 11,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            autoQuit.Checked += (s, e) => { r.AutoQuitKnown = true; LauncherService.Instance.Save(); };
            autoQuit.Unchecked += (s, e) => { r.AutoQuitKnown = false; LauncherService.Instance.Save(); };
            cfg.Children.Add(autoQuit);

            left.Children.Add(cfg);
            grid.Children.Add(left);
            Grid.SetColumn(left, 0);

            // right column: delete
            var del = new Button { Style = (Style)FindResource("GhostButton"), Content = "✕", Padding = new Thickness(10, 6, 10, 6) };
            del.Click += (s, e) =>
            {
                LauncherService.Instance.Rules.Remove(r);
                LauncherService.Instance.Save();
                Stack.Children.Remove(card);
                Ui.Toast("Rule deleted");
            };
            grid.Children.Add(del);
            Grid.SetColumn(del, 1);

            card.Child = grid;
            return card;
        }

        private TextBlock MkLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = (System.Windows.Media.Brush)FindResource("TextMuted"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private void Toggle_Click(object s, RoutedEventArgs e)
        {
            if (Host.Rules.Running)
            {
                Host.Rules.Stop();
                EngineState.Text = "stopped";
                EngineState.Foreground = (System.Windows.Media.Brush)FindResource("TextMuted");
                BtnToggle.Content = "Start engine";
                Ui.Toast("Automation stopped");
            }
            else
            {
                Host.Rules.Start();
                EngineState.Text = "running";
                EngineState.Foreground = (System.Windows.Media.Brush)FindResource("GreenGlow");
                BtnToggle.Content = "Stop engine";
                Ui.Toast("Automation running");
                Ui.Sound(SoundService.Clip.Confirm);
            }
        }
    }
}
