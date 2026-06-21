namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Termrig.Core.Enums;
    using Termrig.Core.Models;

    /// <summary>
    /// Dialog for editing terminal tab settings.
    /// </summary>
    public partial class TerminalTabEditorWindow : Window
    {
        #region Private-Members

        private readonly List<ShellDescriptor> _Shells;
        private readonly List<ColorScheme> _ColorSchemes;
        private readonly List<string> _FontFamilies = new List<string>
        {
            "Use profile font",
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
        private readonly TerminalTabProfile _Original;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the tab editor for the XAML loader.
        /// </summary>
        public TerminalTabEditorWindow()
            : this(null, new Termrig.Core.Services.ShellCatalog().GetSupportedShells(), Termrig.Core.Services.ColorSchemeCatalog.GetSchemes())
        {
        }

        /// <summary>
        /// Instantiate the tab editor.
        /// </summary>
        /// <param name="tab">Existing tab, or null for a new tab.</param>
        /// <param name="shells">Supported shells.</param>
        /// <param name="colorSchemes">Available color schemes.</param>
        /// <exception cref="ArgumentNullException">Thrown when shells or colorSchemes is null.</exception>
        public TerminalTabEditorWindow(TerminalTabProfile? tab, List<ShellDescriptor> shells, List<ColorScheme> colorSchemes)
        {
            ArgumentNullException.ThrowIfNull(shells);
            ArgumentNullException.ThrowIfNull(colorSchemes);

            _Shells = shells;
            _ColorSchemes = colorSchemes;
            _Original = Clone(tab ?? CreateDefaultTab(shells));

            InitializeComponent();
            InitializeForm();
            WireEvents();
        }

        #endregion

        #region Private-Methods

        private static TerminalTabProfile CreateDefaultTab(List<ShellDescriptor> shells)
        {
            ShellType shell = shells.FirstOrDefault(item => item.Shell == ShellType.Cmd)?.Shell
                ?? shells.FirstOrDefault()?.Shell
                ?? (OperatingSystem.IsWindows() ? ShellType.Cmd : ShellType.Bash);
            return new TerminalTabProfile
            {
                Name = "Terminal",
                Shell = shell,
                StartingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };
        }

        private static TerminalTabProfile Clone(TerminalTabProfile tab)
        {
            return tab.Clone();
        }

        private void InitializeForm()
        {
            ShellCombo.ItemsSource = _Shells.Select(item => item.Name).ToList();
            ColorOverrideCombo.ItemsSource = new List<string> { "Use profile color" }
                .Concat(_ColorSchemes.Select(item => item.Name))
                .ToList();
            if (!String.IsNullOrWhiteSpace(_Original.FontFamily) && !_FontFamilies.Contains(_Original.FontFamily))
            {
                _FontFamilies.Add(_Original.FontFamily);
            }

            FontFamilyCombo.ItemsSource = _FontFamilies;

            NameBox.Text = _Original.Name;
            DirectoryBox.Text = _Original.StartingDirectory;
            StartupScriptBox.Text = _Original.StartupScript;
            FontFamilyCombo.SelectedItem = _Original.FontFamily ?? "Use profile font";
            FontSizeBox.Text = _Original.FontSize.HasValue ? _Original.FontSize.Value.ToString("0.##") : String.Empty;
            ScrollbackBufferBox.Text = _Original.ScrollbackBufferSize.HasValue ? _Original.ScrollbackBufferSize.Value.ToString() : String.Empty;
            RecordPtyOutputBox.IsChecked = _Original.RecordPtyOutput;
            PtyRecordingDirectoryBox.Text = _Original.PtyRecordingDirectory;

            ShellDescriptor? selectedShell = _Shells.FirstOrDefault(item => item.Shell == _Original.Shell);
            ShellCombo.SelectedItem = selectedShell == null ? null : selectedShell.Name;
            ColorOverrideCombo.SelectedItem = _Original.ColorSchemeOverride == null ? "Use profile color" : _Original.ColorSchemeOverride.Name;
        }

        private void WireEvents()
        {
            SaveButton.Click += OnSaveClicked;
            CancelButton.Click += OnCancelClicked;
            RecordPtyOutputBox.IsCheckedChanged += OnRecordPtyOutputChanged;
        }

        private void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(NameBox.Text)) return;
            if (!(ShellCombo.SelectedItem is string selectedShellName)) return;

            ShellDescriptor? shell = _Shells.FirstOrDefault(item => item.Name == selectedShellName);
            if (shell == null) return;

            TerminalTabProfile tab = new TerminalTabProfile
            {
                Name = NameBox.Text,
                Shell = shell.Shell,
                StartingDirectory = DirectoryBox.Text ?? String.Empty,
                StartupScript = StartupScriptBox.Text ?? String.Empty,
                FontFamily = FontFamilyCombo.SelectedItem is string fontFamily && fontFamily != "Use profile font" ? fontFamily : null,
                FontSize = ParseNullableFontSize(FontSizeBox.Text),
                ScrollbackBufferSize = ParseNullableBufferSize(ScrollbackBufferBox.Text),
                RecordPtyOutput = RecordPtyOutputBox.IsChecked == true,
                PtyRecordingDirectory = RecordPtyOutputBox.IsChecked == true
                    ? ResolvePtyRecordingDirectory(PtyRecordingDirectoryBox.Text)
                    : (PtyRecordingDirectoryBox.Text ?? String.Empty)
            };

            if (ColorOverrideCombo.SelectedItem is string selectedColor && selectedColor != "Use profile color")
            {
                ColorScheme? scheme = _ColorSchemes.FirstOrDefault(item => item.Name == selectedColor);
                if (scheme != null)
                {
                    tab.ColorSchemeOverride = new ColorScheme
                    {
                        Name = scheme.Name,
                        Background = scheme.Background,
                        Foreground = scheme.Foreground
                    };
                }
            }

            Close(tab);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }

        private void OnRecordPtyOutputChanged(object? sender, RoutedEventArgs e)
        {
            if (RecordPtyOutputBox.IsChecked == true && String.IsNullOrWhiteSpace(PtyRecordingDirectoryBox.Text))
            {
                PtyRecordingDirectoryBox.Text = GetDefaultPtyRecordingDirectory();
            }
        }

        private static double? ParseNullableFontSize(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            if (Double.TryParse(value, out double fontSize)) return fontSize;
            return null;
        }

        private static int? ParseNullableBufferSize(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            if (Int32.TryParse(value, out int bufferSize)) return bufferSize;
            return null;
        }

        private static string ResolvePtyRecordingDirectory(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? GetDefaultPtyRecordingDirectory() : value;
        }

        private static string GetDefaultPtyRecordingDirectory()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Termrig",
                "pty-recordings");
        }

        #endregion
    }
}
