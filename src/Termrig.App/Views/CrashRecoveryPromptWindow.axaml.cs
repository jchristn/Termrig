namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Interactivity;

    /// <summary>
    /// Confirmation dialog shown when Termrig can restore workspaces after an unclean shutdown.
    /// </summary>
    public partial class CrashRecoveryPromptWindow : Window
    {
        /// <summary>
        /// Instantiate the crash recovery prompt for the XAML loader.
        /// </summary>
        public CrashRecoveryPromptWindow()
            : this(0)
        {
        }

        /// <summary>
        /// Instantiate the crash recovery prompt.
        /// </summary>
        /// <param name="workspaceCount">Number of workspace instances available to restore.</param>
        public CrashRecoveryPromptWindow(int workspaceCount)
        {
            InitializeComponent();
            MessageText.Text = BuildMessage(workspaceCount);
            RestoreButton.Click += OnRestoreClicked;
            DontRestoreButton.Click += OnDontRestoreClicked;
            KeyDown += OnKeyDown;
        }

        private static string BuildMessage(int workspaceCount)
        {
            string workspaceText = workspaceCount == 1 ? "1 workspace" : workspaceCount + " workspaces";
            return "Termrig did not shut down cleanly. Reopen " + workspaceText + " from the previous session?";
        }

        private void OnRestoreClicked(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void OnDontRestoreClicked(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close(false);
            }
        }
    }
}
