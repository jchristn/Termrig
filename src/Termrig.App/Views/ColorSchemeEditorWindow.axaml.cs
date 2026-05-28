namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Media;
    using System;
    using Termrig.Core.Models;
    using Termrig.Core.Services;

    /// <summary>
    /// Dialog for adding or editing a global color scheme.
    /// </summary>
    public partial class ColorSchemeEditorWindow : Window
    {
        #region Private-Members

        private readonly ColorScheme _Original;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the color scheme editor for the XAML loader.
        /// </summary>
        public ColorSchemeEditorWindow()
            : this(null)
        {
        }

        /// <summary>
        /// Instantiate the color scheme editor.
        /// </summary>
        /// <param name="scheme">Existing scheme, or null for a new scheme.</param>
        public ColorSchemeEditorWindow(ColorScheme? scheme)
        {
            _Original = scheme == null
                ? new ColorScheme { Name = "New Scheme", Background = "#101419", Foreground = "#E6EDF3" }
                : ColorSchemeCatalog.Clone(scheme);

            InitializeComponent();
            InitializeForm();
            WireEvents();
        }

        #endregion

        #region Private-Methods

        private void InitializeForm()
        {
            NameBox.Text = _Original.Name;
            BackgroundPicker.Color = ParseColor(_Original.Background);
            ForegroundPicker.Color = ParseColor(_Original.Foreground);
        }

        private void WireEvents()
        {
            SaveButton.Click += OnSaveClicked;
            CancelButton.Click += OnCancelClicked;
        }

        private void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(NameBox.Text)) return;

            Close(new ColorScheme
            {
                Name = NameBox.Text,
                Background = ToHex(BackgroundPicker.Color),
                Foreground = ToHex(ForegroundPicker.Color)
            });
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private static Color ParseColor(string value)
        {
            try
            {
                return Color.Parse(value);
            }
            catch (FormatException)
            {
                return Color.Parse("#101419");
            }
        }

        private static string ToHex(Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }

        #endregion
    }
}
