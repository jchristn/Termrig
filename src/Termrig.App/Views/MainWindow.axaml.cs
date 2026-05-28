namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Input;
    using Avalonia.Media;
    using Avalonia.VisualTree;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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
        private readonly ColorSchemeStore _ColorSchemeStore = new ColorSchemeStore();
        private readonly ShellCatalog _ShellCatalog = new ShellCatalog();
        private const string RepositoryUrl = "https://github.com/jchristn/Termrig";
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
        private List<ColorScheme> _ColorSchemes = ColorSchemeCatalog.GetSchemes();
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
            GitHubButton.Click += OnGitHubClicked;
            AddSchemeButton.Click += OnAddSchemeClicked;
            EditSchemeButton.Click += OnEditSchemeClicked;
            DeleteSchemeButton.Click += OnDeleteSchemeClicked;
            ResetSchemesButton.Click += OnResetSchemesClicked;
            AddTabButton.Click += OnAddTabClicked;
            EditTabButton.Click += OnEditTabClicked;
            DeleteTabButton.Click += OnDeleteTabClicked;
            MoveTabUpButton.Click += OnMoveTabUpClicked;
            MoveTabDownButton.Click += OnMoveTabDownClicked;
            TabsList.Tapped += OnTabsListTapped;
            ProfileList.SelectionChanged += OnProfileSelectionChanged;
            GlobalSchemeCombo.SelectionChanged += OnGlobalSchemeChanged;
            SchemeBackgroundPicker.ColorChanged += OnColorPickerChanged;
            SchemeForegroundPicker.ColorChanged += OnColorPickerChanged;
        }

        private void InitializeLists()
        {
            RefreshColorSchemeList(null);
            ProfileFontFamilyCombo.ItemsSource = _FontFamilies;
        }

        private async void LoadProfilesAsync()
        {
            _ColorSchemes = await _ColorSchemeStore.LoadAsync(CancellationToken.None).ConfigureAwait(true);
            RefreshColorSchemeList(null);

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
                _SelectedProfile.GlobalColorScheme = CloneScheme(FindSchemeByName(selectedScheme));
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
            TerminalWorkspaceWindow window = new TerminalWorkspaceWindow(_SelectedProfile, _ProfileStore, _ShellCatalog, _ColorSchemes);
            window.Show();
        }

        private void OnGitHubClicked(object? sender, RoutedEventArgs e)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = RepositoryUrl,
                UseShellExecute = true
            };
            Process? process = Process.Start(startInfo);
            process?.Dispose();
        }

        private async void OnAddSchemeClicked(object? sender, RoutedEventArgs e)
        {
            ColorSchemeEditorWindow editor = new ColorSchemeEditorWindow();
            ColorScheme? scheme = await editor.ShowDialog<ColorScheme?>(this).ConfigureAwait(true);
            if (scheme == null) return;

            string uniqueName = GetUniqueSchemeName(scheme.Name, null);
            scheme.Name = uniqueName;
            _ColorSchemes.Add(scheme);
            await _ColorSchemeStore.SaveAsync(_ColorSchemes, CancellationToken.None).ConfigureAwait(true);
            RefreshColorSchemeList(scheme.Name);
            ApplySelectedGlobalScheme();
        }

        private async void OnEditSchemeClicked(object? sender, RoutedEventArgs e)
        {
            if (!(GlobalSchemeCombo.SelectedItem is string selectedScheme)) return;
            Int32 index = _ColorSchemes.FindIndex(item => item.Name == selectedScheme);
            if (index < 0) return;

            ColorSchemeEditorWindow editor = new ColorSchemeEditorWindow(_ColorSchemes[index]);
            ColorScheme? scheme = await editor.ShowDialog<ColorScheme?>(this).ConfigureAwait(true);
            if (scheme == null) return;

            scheme.Name = GetUniqueSchemeName(scheme.Name, index);
            _ColorSchemes[index] = scheme;
            await _ColorSchemeStore.SaveAsync(_ColorSchemes, CancellationToken.None).ConfigureAwait(true);
            RefreshProfilesUsingScheme(selectedScheme, scheme);
            await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            RefreshColorSchemeList(scheme.Name);
            ApplySelectedGlobalScheme();
        }

        private async void OnDeleteSchemeClicked(object? sender, RoutedEventArgs e)
        {
            if (_ColorSchemes.Count <= 1) return;
            if (!(GlobalSchemeCombo.SelectedItem is string selectedScheme)) return;

            Int32 index = _ColorSchemes.FindIndex(item => item.Name == selectedScheme);
            if (index < 0) return;

            _ColorSchemes.RemoveAt(index);
            await _ColorSchemeStore.SaveAsync(_ColorSchemes, CancellationToken.None).ConfigureAwait(true);

            ColorScheme fallback = _ColorSchemes[Math.Min(index, _ColorSchemes.Count - 1)];
            RefreshProfilesUsingScheme(selectedScheme, fallback);
            await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            RefreshColorSchemeList(fallback.Name);
            ApplySelectedGlobalScheme();
        }

        private async void OnResetSchemesClicked(object? sender, RoutedEventArgs e)
        {
            string? selectedName = GlobalSchemeCombo.SelectedItem as string;
            _ColorSchemes = await _ColorSchemeStore.ResetDefaultsAsync(CancellationToken.None).ConfigureAwait(true);
            string replacementName = _ColorSchemes.Any(item => item.Name == selectedName) ? selectedName! : _ColorSchemes[0].Name;
            ReconcileProfilesWithAvailableSchemes(_ColorSchemes[0]);
            await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            RefreshColorSchemeList(replacementName);
            ApplySelectedGlobalScheme();
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
            await EditSelectedTabAsync().ConfigureAwait(true);
        }

        private async void OnTabsListTapped(object? sender, TappedEventArgs e)
        {
            if (!(e.Source is Control source)) return;
            if (!(source is ListBoxItem) && source.FindAncestorOfType<ListBoxItem>() == null) return;
            await EditSelectedTabAsync().ConfigureAwait(true);
        }

        private async System.Threading.Tasks.Task EditSelectedTabAsync()
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
            ApplySelectedGlobalScheme();
        }

        private void ApplySelectedGlobalScheme()
        {
            if (_SelectedProfile == null) return;
            if (GlobalSchemeCombo.SelectedItem is string selectedScheme)
            {
                ColorScheme scheme = FindSchemeByName(selectedScheme);
                _SelectedProfile.GlobalColorScheme = CloneScheme(scheme);
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

        private void RefreshColorSchemeList(string? selectedName)
        {
            GlobalSchemeCombo.ItemsSource = _ColorSchemes.Select(item => item.Name).ToList();
            if (!String.IsNullOrWhiteSpace(selectedName) && _ColorSchemes.Any(item => item.Name == selectedName))
            {
                GlobalSchemeCombo.SelectedItem = selectedName;
            }
        }

        private ColorScheme FindSchemeByName(string? name)
        {
            ColorScheme? scheme = _ColorSchemes.FirstOrDefault(item => item.Name == name);
            return scheme ?? _ColorSchemes[0];
        }

        private static ColorScheme CloneScheme(ColorScheme scheme)
        {
            return ColorSchemeCatalog.Clone(scheme);
        }

        private string GetUniqueSchemeName(string requestedName, Int32? editingIndex)
        {
            string baseName = String.IsNullOrWhiteSpace(requestedName) ? "New Scheme" : requestedName.Trim();
            string candidate = baseName;
            Int32 suffix = 2;
            while (_ColorSchemes.Where((item, index) => !editingIndex.HasValue || index != editingIndex.Value).Any(item => item.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = baseName + " " + suffix;
                suffix++;
            }

            return candidate;
        }

        private void RefreshProfilesUsingScheme(string previousName, ColorScheme replacement)
        {
            foreach (TerminalProfile profile in _Profiles)
            {
                if (profile.GlobalColorScheme.Name.Equals(previousName, StringComparison.OrdinalIgnoreCase))
                {
                    profile.GlobalColorScheme = CloneScheme(replacement);
                }

                foreach (TerminalTabProfile tab in profile.Tabs)
                {
                    if (tab.ColorSchemeOverride != null && tab.ColorSchemeOverride.Name.Equals(previousName, StringComparison.OrdinalIgnoreCase))
                    {
                        tab.ColorSchemeOverride = CloneScheme(replacement);
                    }
                }
            }

            RefreshEditor();
        }

        private void ReconcileProfilesWithAvailableSchemes(ColorScheme fallback)
        {
            foreach (TerminalProfile profile in _Profiles)
            {
                ColorScheme? globalScheme = _ColorSchemes.FirstOrDefault(item => item.Name.Equals(profile.GlobalColorScheme.Name, StringComparison.OrdinalIgnoreCase));
                profile.GlobalColorScheme = CloneScheme(globalScheme ?? fallback);

                foreach (TerminalTabProfile tab in profile.Tabs)
                {
                    if (tab.ColorSchemeOverride == null) continue;
                    ColorScheme? overrideScheme = _ColorSchemes.FirstOrDefault(item => item.Name.Equals(tab.ColorSchemeOverride.Name, StringComparison.OrdinalIgnoreCase));
                    tab.ColorSchemeOverride = overrideScheme == null ? null : CloneScheme(overrideScheme);
                }
            }

            RefreshEditor();
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
