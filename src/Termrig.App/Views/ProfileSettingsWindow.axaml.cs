namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Interactivity;
    using Avalonia.Media;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Termrig.App.Services;
    using Termrig.Core.Models;
    using Termrig.Core.Services;

    /// <summary>
    /// Settings for one profile: name, folder, startup, colors and font. Closes with a
    /// <see cref="ProfileSettingsResult"/> on Save, or null on Cancel.
    /// </summary>
    public partial class ProfileSettingsWindow : Window
    {
        #region Public-Members

        /// <summary>
        /// Font choices offered for profiles and tabs; the first entry means "use the default".
        /// </summary>
        public static readonly IReadOnlyList<string> FontFamilies = new List<string>
        {
            DefaultFontLabel,
            "Cascadia Mono",
            "Cascadia Code",
            "Consolas",
            "Courier New",
            "JetBrains Mono",
            "Menlo",
            "Monaco",
            "DejaVu Sans Mono",
            "Fira Code"
        };

        /// <summary>
        /// Font combo entry meaning "use the default terminal font".
        /// </summary>
        public const string DefaultFontLabel = "Default terminal font";

        #endregion

        #region Private-Members

        private const string NoFolderLabel = "No folder";
        private const double MinimumFontSize = 6;
        private const double MaximumFontSize = 72;
        private readonly TerminalProfile _Profile;
        private readonly IReadOnlyList<ProfileFolder> _Folders;
        private readonly IColorSchemeHost? _Host;
        private bool _IsLoading = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the dialog for the XAML loader.
        /// </summary>
        public ProfileSettingsWindow()
            : this(new TerminalProfile { Name = "Default" }, new List<ProfileFolder>(), null)
        {
        }

        /// <summary>
        /// Instantiate the dialog.
        /// </summary>
        /// <param name="profile">Profile to edit. It is not changed; the result carries the new values.</param>
        /// <param name="folders">Available folders.</param>
        /// <param name="host">Owner of the shared color scheme list, or null to disable scheme management.</param>
        public ProfileSettingsWindow(TerminalProfile profile, IReadOnlyList<ProfileFolder> folders, IColorSchemeHost? host)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(folders);

            _Profile = profile;
            _Folders = folders;
            _Host = host;

            InitializeComponent();
            Title = profile.Name + " | Profile Settings";
            IntroText.Text = "Settings for the profile \"" + profile.Name + "\". Individual tabs can override the colors and font in their own settings.";
            WireEvents();
            Load();
        }

        #endregion

        #region Private-Methods

        private void WireEvents()
        {
            SaveButton.Click += OnSaveClicked;
            CancelButton.Click += delegate { Close(null); };
            SchemeCombo.SelectionChanged += OnSchemeChanged;
            BackgroundPicker.ColorChanged += delegate { UpdatePreview(); };
            ForegroundPicker.ColorChanged += delegate { UpdatePreview(); };
            FontFamilyCombo.SelectionChanged += delegate { UpdatePreview(); };
            AddSchemeButton.Click += OnAddSchemeClicked;
            EditSchemeButton.Click += OnEditSchemeClicked;
            DeleteSchemeButton.Click += OnDeleteSchemeClicked;
            ResetSchemesButton.Click += OnResetSchemesClicked;
            KeyDown += OnKeyDown;

            bool canManageSchemes = _Host != null;
            AddSchemeButton.IsEnabled = canManageSchemes;
            EditSchemeButton.IsEnabled = canManageSchemes;
            DeleteSchemeButton.IsEnabled = canManageSchemes;
            ResetSchemesButton.IsEnabled = canManageSchemes;
        }

        private void Load()
        {
            _IsLoading = true;
            try
            {
                NameBox.Text = _Profile.Name;

                List<string> folders = new List<string> { NoFolderLabel };
                folders.AddRange(_Folders.Select(item => item.Name));
                FolderCombo.ItemsSource = folders;
                ProfileFolder? folder = _Folders.FirstOrDefault(item => item.Id == _Profile.FolderId);
                FolderCombo.SelectedItem = folder?.Name ?? NoFolderLabel;

                AutoOpenBox.IsChecked = _Profile.AutoOpen;

                RefreshSchemeList(_Profile.GlobalColorScheme.Name);
                BackgroundPicker.Color = ParseColor(_Profile.GlobalColorScheme.Background);
                ForegroundPicker.Color = ParseColor(_Profile.GlobalColorScheme.Foreground);

                FontFamilyCombo.ItemsSource = FontFamilies;
                FontFamilyCombo.SelectedItem = !String.IsNullOrWhiteSpace(_Profile.FontFamily) && FontFamilies.Contains(_Profile.FontFamily)
                    ? _Profile.FontFamily
                    : DefaultFontLabel;
                FontSizeBox.Text = _Profile.FontSize.HasValue ? _Profile.FontSize.Value.ToString("0.##") : String.Empty;
            }
            finally
            {
                _IsLoading = false;
            }

            UpdatePreview();
        }

        private void RefreshSchemeList(string? selectedName)
        {
            List<string> names = GetSchemes().Select(item => item.Name).ToList();
            bool wasLoading = _IsLoading;
            _IsLoading = true;
            try
            {
                SchemeCombo.ItemsSource = names;
                string? match = names.FirstOrDefault(item => item.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
                SchemeCombo.SelectedItem = match ?? names.FirstOrDefault();
            }
            finally
            {
                _IsLoading = wasLoading;
            }

            DeleteSchemeButton.IsEnabled = _Host != null && names.Count > 1;
        }

        private IReadOnlyList<ColorScheme> GetSchemes()
        {
            if (_Host != null) return _Host.ColorSchemes;
            return new List<ColorScheme> { _Profile.GlobalColorScheme };
        }

        private ColorScheme? GetSelectedScheme()
        {
            if (!(SchemeCombo.SelectedItem is string name)) return null;
            return GetSchemes().FirstOrDefault(item => item.Name == name);
        }

        private void ApplySchemeColors(ColorScheme? scheme)
        {
            if (scheme == null) return;
            BackgroundPicker.Color = ParseColor(scheme.Background);
            ForegroundPicker.Color = ParseColor(scheme.Foreground);
            UpdatePreview();
        }

        private void OnSchemeChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_IsLoading) return;
            ApplySchemeColors(GetSelectedScheme());
        }

        private async void OnAddSchemeClicked(object? sender, RoutedEventArgs e)
        {
            if (_Host == null) return;
            string? name = await _Host.AddColorSchemeAsync(this).ConfigureAwait(true);
            if (name == null) return;

            RefreshSchemeList(name);
            ApplySchemeColors(GetSelectedScheme());
        }

        private async void OnEditSchemeClicked(object? sender, RoutedEventArgs e)
        {
            if (_Host == null || !(SchemeCombo.SelectedItem is string selected)) return;
            string? name = await _Host.EditColorSchemeAsync(this, selected).ConfigureAwait(true);
            if (name == null) return;

            RefreshSchemeList(name);
            ApplySchemeColors(GetSelectedScheme());
        }

        private async void OnDeleteSchemeClicked(object? sender, RoutedEventArgs e)
        {
            if (_Host == null || !(SchemeCombo.SelectedItem is string selected)) return;
            string? fallback = await _Host.DeleteColorSchemeAsync(this, selected).ConfigureAwait(true);
            if (fallback == null) return;

            RefreshSchemeList(fallback);
            ApplySchemeColors(GetSelectedScheme());
        }

        private async void OnResetSchemesClicked(object? sender, RoutedEventArgs e)
        {
            if (_Host == null) return;
            string? selected = SchemeCombo.SelectedItem as string;
            string? name = await _Host.ResetColorSchemesAsync(this, selected).ConfigureAwait(true);
            if (name == null) return;

            RefreshSchemeList(name);
            ApplySchemeColors(GetSelectedScheme());
        }

        private void UpdatePreview()
        {
            IBrush background = new SolidColorBrush(BackgroundPicker.Color);
            IBrush foreground = new SolidColorBrush(ForegroundPicker.Color);
            string? fontFamily = FontFamilyCombo.SelectedItem is string family && family != DefaultFontLabel ? family : null;
            FontFamily font = TerminalFonts.Resolve(fontFamily);

            PreviewBorder.Background = background;
            foreach (TextBlock line in new[] { PreviewLine1, PreviewLine2 })
            {
                line.Foreground = foreground;
                line.FontFamily = font;
                line.FontSize = 13;
            }
        }

        private void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            string name = (NameBox.Text ?? String.Empty).Trim();
            if (String.IsNullOrWhiteSpace(name))
            {
                ShowError("Enter a profile name.");
                NameBox.Focus();
                return;
            }

            double? fontSize = null;
            string fontSizeText = (FontSizeBox.Text ?? String.Empty).Trim();
            if (!String.IsNullOrEmpty(fontSizeText))
            {
                if (!Double.TryParse(fontSizeText, out double parsed) || parsed < MinimumFontSize || parsed > MaximumFontSize)
                {
                    ShowError("Font size must be a number from " + MinimumFontSize + " to " + MaximumFontSize + ".");
                    FontSizeBox.Focus();
                    return;
                }

                fontSize = parsed;
            }

            ColorScheme scheme = ColorSchemeCatalog.Clone(GetSelectedScheme() ?? _Profile.GlobalColorScheme);
            scheme.Background = ToHex(BackgroundPicker.Color);
            scheme.Foreground = ToHex(ForegroundPicker.Color);

            string folderId = String.Empty;
            if (FolderCombo.SelectedItem is string folderName && folderName != NoFolderLabel)
            {
                folderId = _Folders.FirstOrDefault(item => item.Name == folderName)?.Id ?? String.Empty;
            }

            ProfileSettingsResult result = new ProfileSettingsResult
            {
                Name = name,
                FolderId = folderId,
                AutoOpen = AutoOpenBox.IsChecked == true,
                ColorScheme = scheme,
                FontFamily = FontFamilyCombo.SelectedItem is string family && family != DefaultFontLabel ? family : null,
                FontSize = fontSize
            };
            Close(result);
        }

        private void ShowError(string message)
        {
            MessageText.Text = message;
            MessageText.IsVisible = true;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
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

    /// <summary>
    /// Values chosen in <see cref="ProfileSettingsWindow"/>.
    /// </summary>
    public sealed class ProfileSettingsResult
    {
        /// <summary>
        /// Profile name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Folder identifier, or empty for no folder.
        /// </summary>
        public string FolderId { get; set; } = String.Empty;

        /// <summary>
        /// Open the profile when Termrig starts.
        /// </summary>
        public bool AutoOpen { get; set; } = false;

        /// <summary>
        /// The profile's color scheme, with its background and foreground.
        /// </summary>
        public ColorScheme ColorScheme { get; set; } = new ColorScheme();

        /// <summary>
        /// Font family, or null for the default.
        /// </summary>
        public string? FontFamily { get; set; } = null;

        /// <summary>
        /// Font size, or null for the default.
        /// </summary>
        public double? FontSize { get; set; } = null;
    }

    /// <summary>
    /// Owner of the shared color scheme list. Each operation saves right away and returns the scheme name to select,
    /// or null when nothing changed.
    /// </summary>
    public interface IColorSchemeHost
    {
        /// <summary>
        /// Current color schemes.
        /// </summary>
        IReadOnlyList<ColorScheme> ColorSchemes { get; }

        /// <summary>
        /// Create a color scheme.
        /// </summary>
        /// <param name="owner">Dialog owner.</param>
        /// <returns>New scheme name, or null.</returns>
        Task<string?> AddColorSchemeAsync(Window owner);

        /// <summary>
        /// Edit a color scheme.
        /// </summary>
        /// <param name="owner">Dialog owner.</param>
        /// <param name="name">Scheme name.</param>
        /// <returns>Edited scheme name, or null.</returns>
        Task<string?> EditColorSchemeAsync(Window owner, string name);

        /// <summary>
        /// Delete a color scheme after confirming.
        /// </summary>
        /// <param name="owner">Dialog owner.</param>
        /// <param name="name">Scheme name.</param>
        /// <returns>Name of the scheme that replaces it, or null.</returns>
        Task<string?> DeleteColorSchemeAsync(Window owner, string name);

        /// <summary>
        /// Restore the built-in color schemes after confirming.
        /// </summary>
        /// <param name="owner">Dialog owner.</param>
        /// <param name="selectedName">Currently selected scheme name.</param>
        /// <returns>Scheme name to select, or null.</returns>
        Task<string?> ResetColorSchemesAsync(Window owner, string? selectedName);
    }
}
