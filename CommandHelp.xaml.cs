using System.Windows;
using System.Windows.Input;

namespace DriveUniverse
{
    /// <summary>
    /// A beautiful command reference window. Opens when user says "help" or clicks help.
    /// Shows all voice commands with icons and descriptions.
    /// </summary>
    public partial class CommandHelp : Window
    {
        public CommandHelp()
        {
            InitializeComponent();
            var wa = SystemParameters.WorkArea;
            Left = wa.Right - Width - 20;
            Top = wa.Bottom - Height - 20;
        }

        private void Drag(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
