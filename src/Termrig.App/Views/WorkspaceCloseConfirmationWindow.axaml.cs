namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Interactivity;
    using System;

    /// <summary>
    /// Confirmation dialog shown before closing a terminal workspace.
    /// </summary>
    public partial class WorkspaceCloseConfirmationWindow : Window
    {
        /// <summary>
        /// Instantiate the confirmation dialog for the XAML loader.
        /// </summary>
        public WorkspaceCloseConfirmationWindow()
            : this("workspace", 0)
        {
        }

        /// <summary>
        /// Instantiate the confirmation dialog.
        /// </summary>
        /// <param name="profileName">Profile name displayed in the prompt.</param>
        /// <param name="tabCount">Number of open tabs in the workspace.</param>
        public WorkspaceCloseConfirmationWindow(string profileName, int tabCount)
        {
            InitializeComponent();
            MessageText.Text = BuildMessage(profileName, tabCount);
            CloseWorkspaceButton.Click += OnCloseWorkspaceClicked;
            CancelButton.Click += OnCancelClicked;
            KeyDown += OnKeyDown;
        }

        private static string BuildMessage(string profileName, int tabCount)
        {
            string tabText = tabCount == 1 ? "1 open tab" : tabCount + " open tabs";
            return "This will close \"" + profileName + "\", including all tabs, and stop terminal sessions in its " + tabText + ".";
        }

        private void OnCloseWorkspaceClicked(object? sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            Close(false);
        }
    }
}
