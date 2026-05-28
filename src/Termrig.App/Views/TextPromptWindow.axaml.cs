namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Interactivity;

    /// <summary>
    /// Small text input dialog.
    /// </summary>
    public partial class TextPromptWindow : Window
    {
        /// <summary>
        /// Instantiate the text prompt for the XAML loader.
        /// </summary>
        public TextPromptWindow()
            : this("Prompt", "Value", string.Empty)
        {
        }

        /// <summary>
        /// Instantiate the text prompt.
        /// </summary>
        /// <param name="title">Window title.</param>
        /// <param name="prompt">Prompt text.</param>
        /// <param name="value">Initial value.</param>
        public TextPromptWindow(string title, string prompt, string value)
        {
            InitializeComponent();
            Title = title;
            PromptText.Text = prompt;
            ValueBox.Text = value;
            SaveButton.Click += OnSaveClicked;
            CancelButton.Click += OnCancelClicked;
        }

        private void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            Close(ValueBox.Text);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }
    }
}
