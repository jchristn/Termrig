namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Media;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Termrig.Core.Enums;
    using Termrig.Core.Models;
    using Termrig.Core.Services;

    /// <summary>
    /// Main profile management window.
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Private-Members

        private readonly ProfileStore _ProfileStore = new ProfileStore();
        private readonly ShellCatalog _ShellCatalog = new ShellCatalog();
        private readonly List<ColorScheme> _ColorSchemes = ColorSchemeCatalog.GetSchemes();
        private readonly List<string> _FontFamilies = new List<string>
        {
            "Default terminal font",
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
        private List<TerminalProfile> _Profiles = new List<TerminalProfile>();
        private TerminalProfile? _SelectedProfile = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the main window.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            WireEvents();
            InitializeLists();
            LoadProfilesAsync();
        }

        #endregion

        #region Private-Methods

        private void WireEvents()
        {
            NewProfileButton.Click += OnNewProfileClicked;
            DeleteProfileButton.Click += OnDeleteProfileClicked;
            SaveProfileButton.Click += OnSaveProfileClicked;
            OpenProfileButton.Click += OnOpenProfileClicked;
            AddTabButton.Click += OnAddTabClicked;
            EditTabButton.Click += OnEditTabClicked;
            DeleteTabButton.Click += OnDeleteTabClicked;
            MoveTabUpButton.Click += OnMoveTabUpClicked;
            MoveTabDownButton.Click += OnMoveTabDownClicked;
            ProfileList.SelectionChanged += OnProfileSelectionChanged;
            GlobalSchemeCombo.SelectionChanged += OnGlobalSchemeChanged;
            SchemeBackgroundPicker.ColorChanged += OnColorPickerChanged;
            SchemeForegroundPicker.ColorChanged += OnColorPickerChanged;
        }

        private void InitializeLists()
        {
            GlobalSchemeCombo.ItemsSource = _ColorSchemes.Select(item => item.Name).ToList();
            ProfileFontFamilyCombo.ItemsSource = _FontFamilies;
        }

        private async void LoadProfilesAsync()
        {
            _Profiles = await _ProfileStore.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            if (!_Profiles.Any())
            {
                _Profiles.Add(CreateDefaultProfile());
                await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            }

            RefreshProfiles();
            ProfileList.SelectedIndex = 0;
        }

        private static TerminalProfile CreateDefaultProfile()
        {
            ShellType shell = OperatingSystem.IsWindows() ? ShellType.PowerShell : ShellType.Bash;
            return new TerminalProfile
            {
                Name = "Default",
                Tabs = new List<TerminalTabProfile>
                {
                    new TerminalTabProfile
                    {
                        Name = "Shell",
                        Shell = shell,
                        StartingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    }
                }
            };
        }

        private void RefreshProfiles()
        {
            ProfileList.ItemsSource = _Profiles.Select(item => item.Name).ToList();
        }

        private void RefreshEditor()
        {
            if (_SelectedProfile == null)
            {
                ProfileNameBox.Text = String.Empty;
                TabsList.ItemsSource = null;
                return;
            }

            ProfileNameBox.Text = _SelectedProfile.Name;
            GlobalSchemeCombo.SelectedItem = _SelectedProfile.GlobalColorScheme.Name;
            SchemeNameBox.Text = _SelectedProfile.GlobalColorScheme.Name;
            SchemeBackgroundPicker.Color = ParseColor(_SelectedProfile.GlobalColorScheme.Background);
            SchemeForegroundPicker.Color = ParseColor(_SelectedProfile.GlobalColorScheme.Foreground);
            ProfileFontFamilyCombo.SelectedItem = _SelectedProfile.FontFamily ?? "Default terminal font";
            ProfileFontSizeBox.Text = _SelectedProfile.FontSize.HasValue ? _SelectedProfile.FontSize.Value.ToString("0.##") : String.Empty;
            RefreshTabs();
        }

        private void RefreshTabs()
        {
            if (_SelectedProfile == null)
            {
                TabsList.ItemsSource = null;
                return;
            }

            TabsList.ItemsSource = _SelectedProfile.Tabs;
        }

        private void ApplyEditorToProfile()
        {
            if (_SelectedProfile == null) return;
            if (!String.IsNullOrWhiteSpace(ProfileNameBox.Text)) _SelectedProfile.Name = ProfileNameBox.Text;
            if (GlobalSchemeCombo.SelectedItem is string selectedScheme)
            {
                _SelectedProfile.GlobalColorScheme = ColorSchemeCatalog.FindByName(selectedScheme);
            }

            if (!String.IsNullOrWhiteSpace(SchemeNameBox.Text)) _SelectedProfile.GlobalColorScheme.Name = SchemeNameBox.Text;
            _SelectedProfile.GlobalColorScheme.Background = ToHex(SchemeBackgroundPicker.Color);
            _SelectedProfile.GlobalColorScheme.Foreground = ToHex(SchemeForegroundPicker.Color);
            _SelectedProfile.FontFamily = ProfileFontFamilyCombo.SelectedItem is string fontFamily && fontFamily != "Default terminal font" ? fontFamily : null;
            if (String.IsNullOrWhiteSpace(ProfileFontSizeBox.Text))
            {
                _SelectedProfile.FontSize = null;
            }
            else if (Double.TryParse(ProfileFontSizeBox.Text, out double fontSize))
            {
                _SelectedProfile.FontSize = fontSize;
            }
        }

        private async void OnSaveProfileClicked(object? sender, RoutedEventArgs e)
        {
            ApplyEditorToProfile();
            await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            RefreshProfiles();
        }

        private void OnNewProfileClicked(object? sender, RoutedEventArgs e)
        {
            TerminalProfile profile = CreateDefaultProfile();
            profile.Name = "Profile " + (_Profiles.Count + 1);
            _Profiles.Add(profile);
            RefreshProfiles();
            ProfileList.SelectedIndex = _Profiles.Count - 1;
        }

        private async void OnDeleteProfileClicked(object? sender, RoutedEventArgs e)
        {
            Int32 index = ProfileList.SelectedIndex;
            if (index < 0 || index >= _Profiles.Count) return;
            _Profiles.RemoveAt(index);
            await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            RefreshProfiles();
            ProfileList.SelectedIndex = _Profiles.Count > 0 ? Math.Min(index, _Profiles.Count - 1) : -1;
        }

        private void OnOpenProfileClicked(object? sender, RoutedEventArgs e)
        {
            ApplyEditorToProfile();
            if (_SelectedProfile == null) return;
            TerminalWorkspaceWindow window = new TerminalWorkspaceWindow(_SelectedProfile, _ProfileStore, _ShellCatalog);
            window.Show();
        }

        private async void OnAddTabClicked(object? sender, RoutedEventArgs e)
        {
            if (_SelectedProfile == null) return;
            TerminalTabEditorWindow editor = new TerminalTabEditorWindow(null, _ShellCatalog.GetSupportedShells(), _ColorSchemes);
            TerminalTabProfile? tab = await editor.ShowDialog<TerminalTabProfile?>(this).ConfigureAwait(true);
            if (tab == null) return;
            _SelectedProfile.Tabs.Add(tab);
            RefreshTabs();
        }

        private async void OnEditTabClicked(object? sender, RoutedEventArgs e)
        {
            if (_SelectedProfile == null) return;
            Int32 index = TabsList.SelectedIndex;
            if (index < 0 || index >= _SelectedProfile.Tabs.Count) return;

            TerminalTabEditorWindow editor = new TerminalTabEditorWindow(_SelectedProfile.Tabs[index], _ShellCatalog.GetSupportedShells(), _ColorSchemes);
            TerminalTabProfile? tab = await editor.ShowDialog<TerminalTabProfile?>(this).ConfigureAwait(true);
            if (tab == null) return;
            _SelectedProfile.Tabs[index] = tab;
            RefreshTabs();
        }

        private void OnDeleteTabClicked(object? sender, RoutedEventArgs e)
        {
            if (_SelectedProfile == null) return;
            Int32 index = TabsList.SelectedIndex;
            if (index < 0 || index >= _SelectedProfile.Tabs.Count) return;
            _SelectedProfile.Tabs.RemoveAt(index);
            RefreshTabs();
        }

        private void OnMoveTabUpClicked(object? sender, RoutedEventArgs e)
        {
            MoveSelectedTab(-1);
        }

        private void OnMoveTabDownClicked(object? sender, RoutedEventArgs e)
        {
            MoveSelectedTab(1);
        }

        private void OnProfileSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            Int32 index = ProfileList.SelectedIndex;
            _SelectedProfile = index >= 0 && index < _Profiles.Count ? _Profiles[index] : null;
            RefreshEditor();
        }

        private void OnGlobalSchemeChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_SelectedProfile == null) return;
            if (GlobalSchemeCombo.SelectedItem is string selectedScheme)
            {
                ColorScheme scheme = ColorSchemeCatalog.FindByName(selectedScheme);
                _SelectedProfile.GlobalColorScheme = scheme;
                SchemeNameBox.Text = scheme.Name;
                SchemeBackgroundPicker.Color = ParseColor(scheme.Background);
                SchemeForegroundPicker.Color = ParseColor(scheme.Foreground);
            }
        }

        private void OnColorPickerChanged(object? sender, ColorChangedEventArgs e)
        {
            ApplyEditorToProfile();
        }

        private void MoveSelectedTab(Int32 offset)
        {
            if (_SelectedProfile == null) return;
            Int32 index = TabsList.SelectedIndex;
            Int32 newIndex = index + offset;
            if (index < 0 || index >= _SelectedProfile.Tabs.Count) return;
            if (newIndex < 0 || newIndex >= _SelectedProfile.Tabs.Count) return;

            TerminalTabProfile tab = _SelectedProfile.Tabs[index];
            _SelectedProfile.Tabs.RemoveAt(index);
            _SelectedProfile.Tabs.Insert(newIndex, tab);
            RefreshTabs();
            TabsList.SelectedIndex = newIndex;
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
