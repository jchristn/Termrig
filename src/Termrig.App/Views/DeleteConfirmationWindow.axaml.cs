namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Interactivity;

    /// <summary>
    /// Confirmation dialog shown before deleting saved configuration.
    /// </summary>
    public partial class DeleteConfirmationWindow : Window
    {
        /// <summary>
        /// Instantiate the confirmation dialog for the XAML loader.
        /// </summary>
        public DeleteConfirmationWindow()
            : this("Confirm delete", "Delete item?", "This item will be deleted.", "Delete")
        {
        }

        /// <summary>
        /// Instantiate the confirmation dialog.
        /// </summary>
        /// <param name="title">Window title.</param>
        /// <param name="heading">Dialog heading.</param>
        /// <param name="message">Dialog message.</param>
        /// <param name="deleteButtonText">Confirm button text.</param>
        public DeleteConfirmationWindow(string title, string heading, string message, string deleteButtonText)
        {
            InitializeComponent();
            Title = title;
            HeadingText.Text = heading;
            MessageText.Text = message;
            DeleteButton.Content = deleteButtonText;
            DeleteButton.Click += OnDeleteClicked;
            CancelButton.Click += OnCancelClicked;
            KeyDown += OnKeyDown;
        }

        private void OnDeleteClicked(object? sender, RoutedEventArgs e)
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
