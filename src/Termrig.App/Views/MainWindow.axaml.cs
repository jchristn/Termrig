namespace Termrig.App.Views
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Input;
    using Avalonia.Media;
    using Avalonia.Threading;
    using Avalonia.VisualTree;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
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
        private TerminalProfile? _DraggedProfile = null;
        private TerminalTabProfile? _DraggedTab = null;
        private TerminalProfile? _DraggedTabSourceProfile = null;
        private Control? _DraggedControl = null;
        private Control? _DropTargetControl = null;
        private TerminalProfile? _PendingDraggedProfile = null;
        private TerminalTabProfile? _PendingDraggedTab = null;
        private TerminalProfile? _PendingDraggedTabSourceProfile = null;
        private ListBoxItem? _PendingDragItem = null;
        private PointerPressedEventArgs? _PendingDragPressedEvent = null;
        private Point _PendingDragStartPoint;
        private bool _IsStartingDrag = false;
        private bool _DropHandled = false;
        private int? _LastProfileDropIndex = null;
        private TerminalProfile? _LastProfileDropTargetProfile = null;
        private int? _LastTabDropIndex = null;
        private const string DraggingItemClass = "draggingItem";
        private const string DropTargetItemClass = "dropTargetItem";
        private const double DragStartThreshold = 4;
        private readonly List<TerminalWorkspaceWindow> _WorkspaceWindows = new List<TerminalWorkspaceWindow>();
        private readonly TaskCompletionSource<bool> _ProfilesLoaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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
            ProfileList.DoubleTapped += OnProfileListDoubleTapped;
            TabsList.DoubleTapped += OnTabsListDoubleTapped;
            ProfileList.SelectionChanged += OnProfileSelectionChanged;
            GlobalSchemeCombo.SelectionChanged += OnGlobalSchemeChanged;
            SchemeBackgroundPicker.ColorChanged += OnColorPickerChanged;
            SchemeForegroundPicker.ColorChanged += OnColorPickerChanged;
            ProfileList.AddHandler(PointerPressedEvent, OnProfileListPointerPressed, RoutingStrategies.Bubble, true);
            TabsList.AddHandler(PointerPressedEvent, OnTabsListPointerPressed, RoutingStrategies.Bubble, true);
            ProfileList.AddHandler(PointerMovedEvent, OnProfileListPointerMoved, RoutingStrategies.Bubble, true);
            TabsList.AddHandler(PointerMovedEvent, OnTabsListPointerMoved, RoutingStrategies.Bubble, true);
            ProfileList.AddHandler(PointerReleasedEvent, OnListPointerReleased, RoutingStrategies.Bubble, true);
            TabsList.AddHandler(PointerReleasedEvent, OnListPointerReleased, RoutingStrategies.Bubble, true);
            DragDrop.SetAllowDrop(ProfileList, true);
            DragDrop.SetAllowDrop(TabsList, true);
            DragDrop.AddDragOverHandler(ProfileList, OnProfileListDragOver);
            DragDrop.AddDropHandler(ProfileList, OnProfileListDrop);
            DragDrop.AddDragLeaveHandler(ProfileList, OnListDragLeave);
            DragDrop.AddDragOverHandler(TabsList, OnTabsListDragOver);
            DragDrop.AddDropHandler(TabsList, OnTabsListDrop);
            DragDrop.AddDragLeaveHandler(TabsList, OnListDragLeave);
        }

        private void InitializeLists()
        {
            RefreshColorSchemeList(null);
            ProfileFontFamilyCombo.ItemsSource = _FontFamilies;
        }

        private async void LoadProfilesAsync()
        {
            try
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
                _ProfilesLoaded.TrySetResult(true);
            }
            catch (Exception exception)
            {
                _ProfilesLoaded.TrySetException(exception);
                throw;
            }
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
            ProfileList.ItemsSource = null;
            ProfileList.ItemsSource = _Profiles;
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
            TabsList.ItemsSource = null;
            if (_SelectedProfile == null)
            {
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
            string? selectedProfileId = _SelectedProfile?.Id;
            ApplyEditorToProfile();
            await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            RefreshProfiles();
            RestoreSelectedProfile(selectedProfileId);
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
            OpenWorkspace(_SelectedProfile);
        }

        private void OnProfileListDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (!(e.Source is Control source)) return;
            if (!(source is ListBoxItem) && source.FindAncestorOfType<ListBoxItem>() == null) return;

            ApplyEditorToProfile();
            if (_SelectedProfile == null) return;
            OpenWorkspace(_SelectedProfile);
        }

        /// <summary>
        /// Open a profile workspace by profile name.
        /// </summary>
        /// <param name="profileName">Profile name.</param>
        /// <returns>True if the profile was found and opened.</returns>
        public async Task<bool> OpenProfileByNameAsync(string profileName)
        {
            if (String.IsNullOrWhiteSpace(profileName)) return false;
            await _ProfilesLoaded.Task.ConfigureAwait(true);

            TerminalProfile? profile = _Profiles.FirstOrDefault(item => item.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));
            if (profile == null) return false;

            OpenWorkspace(profile);
            return true;
        }

        /// <summary>
        /// Close open workspace windows for a profile.
        /// </summary>
        /// <param name="profileName">Profile name.</param>
        /// <returns>True if at least one workspace was closed.</returns>
        public bool CloseProfileWorkspaces(string profileName)
        {
            if (String.IsNullOrWhiteSpace(profileName)) return false;

            List<TerminalWorkspaceWindow> windows = _WorkspaceWindows
                .Where(item => item.ProfileName.Equals(profileName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (TerminalWorkspaceWindow window in windows)
            {
                window.Close();
            }

            return windows.Count > 0;
        }

        private void OpenWorkspace(TerminalProfile profile)
        {
            TerminalWorkspaceWindow window = new TerminalWorkspaceWindow(profile, _ProfileStore, _ShellCatalog, _ColorSchemes);
            _WorkspaceWindows.Add(window);
            window.Closed += delegate
            {
                _WorkspaceWindows.Remove(window);
            };
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
            ApplyEditorToProfile();

            TerminalTabEditorWindow editor = new TerminalTabEditorWindow(null, _ShellCatalog.GetSupportedShells(), _ColorSchemes);
            TerminalTabProfile? tab = await editor.ShowDialog<TerminalTabProfile?>(this).ConfigureAwait(true);
            if (tab == null) return;
            _SelectedProfile.Tabs.Add(tab);
            RefreshTabs();
            TabsList.SelectedIndex = _SelectedProfile.Tabs.Count - 1;
        }

        private async void OnEditTabClicked(object? sender, RoutedEventArgs e)
        {
            await EditSelectedTabAsync().ConfigureAwait(true);
        }

        private async void OnTabsListDoubleTapped(object? sender, TappedEventArgs e)
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

        private void OnProfileListPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(ProfileList).Properties.IsLeftButtonPressed) return;
            if (!(e.Source is Control source)) return;
            if (source is Button || source.FindAncestorOfType<Button>() != null) return;
            ListBoxItem? item = GetListBoxItem(source);
            if (!(item?.DataContext is TerminalProfile profile)) return;

            ClearPendingDrag();
            _PendingDraggedProfile = profile;
            _PendingDragItem = item;
            _PendingDragPressedEvent = e;
            _PendingDragStartPoint = e.GetPosition(ProfileList);
        }

        private void OnTabsListPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_SelectedProfile == null) return;
            if (!e.GetCurrentPoint(TabsList).Properties.IsLeftButtonPressed) return;
            if (!(e.Source is Control source)) return;
            if (source is Button || source.FindAncestorOfType<Button>() != null) return;
            ListBoxItem? item = GetListBoxItem(source);
            if (!(item?.DataContext is TerminalTabProfile tab)) return;

            ClearPendingDrag();
            _PendingDraggedTab = tab;
            _PendingDraggedTabSourceProfile = _SelectedProfile;
            _PendingDragItem = item;
            _PendingDragPressedEvent = e;
            _PendingDragStartPoint = e.GetPosition(TabsList);
        }

        private async void OnProfileListPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_PendingDraggedProfile == null || _PendingDragItem == null) return;
            if (!ShouldStartDrag(e, ProfileList)) return;

            _IsStartingDrag = true;
            _DraggedProfile = _PendingDraggedProfile;
            _DraggedTab = null;
            _DraggedTabSourceProfile = null;
            _DropHandled = false;
            ClearLastDropTargets();
            ListBoxItem dragItem = _PendingDragItem;
            PointerPressedEventArgs? triggerEvent = _PendingDragPressedEvent;
            if (triggerEvent == null) return;
            ClearPendingDrag(e);
            MarkDraggedControl(dragItem);
            try
            {
                await FlushDragVisualAsync().ConfigureAwait(true);
                DataTransfer data = new DataTransfer();
                data.Add(DataTransferItem.CreateText("termrig-profile:" + _DraggedProfile.Id));
                DragDropEffects result = await DragDrop.DoDragDropAsync(triggerEvent, data, DragDropEffects.Move).ConfigureAwait(true);
                if (!_DropHandled && result == DragDropEffects.Move && _LastProfileDropIndex.HasValue)
                {
                    await MoveDraggedProfileAsync(_LastProfileDropIndex.Value).ConfigureAwait(true);
                }
            }
            finally
            {
                _IsStartingDrag = false;
                ClearDragVisuals();
                ClearLastDropTargets();
                _DropHandled = false;
                _DraggedProfile = null;
            }
        }

        private async void OnTabsListPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_PendingDraggedTab == null || _PendingDraggedTabSourceProfile == null || _PendingDragItem == null) return;
            if (!ShouldStartDrag(e, TabsList)) return;

            _IsStartingDrag = true;
            _DraggedProfile = null;
            _DraggedTab = _PendingDraggedTab;
            _DraggedTabSourceProfile = _PendingDraggedTabSourceProfile;
            _DropHandled = false;
            ClearLastDropTargets();
            ListBoxItem dragItem = _PendingDragItem;
            PointerPressedEventArgs? triggerEvent = _PendingDragPressedEvent;
            if (triggerEvent == null) return;
            ClearPendingDrag(e);
            MarkDraggedControl(dragItem);
            try
            {
                await FlushDragVisualAsync().ConfigureAwait(true);
                DataTransfer data = new DataTransfer();
                data.Add(DataTransferItem.CreateText("termrig-tab:" + _DraggedTabSourceProfile.Id + ":" + _DraggedTabSourceProfile.Tabs.IndexOf(_DraggedTab)));
                DragDropEffects result = await DragDrop.DoDragDropAsync(triggerEvent, data, DragDropEffects.Move).ConfigureAwait(true);
                if (!_DropHandled && result == DragDropEffects.Move)
                {
                    if (_LastTabDropIndex.HasValue)
                    {
                        await MoveDraggedTabToProfileAsync(_SelectedProfile, _LastTabDropIndex.Value).ConfigureAwait(true);
                    }
                    else if (_LastProfileDropTargetProfile != null)
                    {
                        await MoveDraggedTabToProfileAsync(_LastProfileDropTargetProfile, _LastProfileDropTargetProfile.Tabs.Count).ConfigureAwait(true);
                    }
                }
            }
            finally
            {
                _IsStartingDrag = false;
                ClearDragVisuals();
                ClearLastDropTargets();
                _DropHandled = false;
                _DraggedTab = null;
                _DraggedTabSourceProfile = null;
            }
        }

        private void OnListPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            ClearPendingDrag(e);
        }

        private void OnProfileListDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = _DraggedProfile != null || _DraggedTab != null ? DragDropEffects.Move : DragDropEffects.None;
            if (_DraggedProfile != null)
            {
                _LastProfileDropIndex = GetProfileDropIndex(e);
                _LastProfileDropTargetProfile = null;
                _LastTabDropIndex = null;
            }
            else if (_DraggedTab != null)
            {
                _LastProfileDropIndex = null;
                _LastProfileDropTargetProfile = GetDropListBoxItem(e)?.DataContext as TerminalProfile;
                _LastTabDropIndex = null;
            }

            MarkDropTarget(GetDropListBoxItem(e), e.DragEffects != DragDropEffects.None);
            e.Handled = true;
        }

        private async void OnProfileListDrop(object? sender, DragEventArgs e)
        {
            if (_DraggedProfile != null)
            {
                await DropProfileAsync(e).ConfigureAwait(true);
                return;
            }

            if (_DraggedTab != null && _DraggedTabSourceProfile != null)
            {
                await DropTabOntoProfileAsync(e).ConfigureAwait(true);
            }

            ClearDropTarget();
        }

        private void OnTabsListDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = _DraggedTab != null && _DraggedTabSourceProfile != null ? DragDropEffects.Move : DragDropEffects.None;
            if (_DraggedTab != null)
            {
                _LastProfileDropIndex = null;
                _LastProfileDropTargetProfile = null;
                _LastTabDropIndex = GetTabDropIndex(e);
            }

            MarkDropTarget(GetDropListBoxItem(e), e.DragEffects != DragDropEffects.None);
            e.Handled = true;
        }

        private async void OnTabsListDrop(object? sender, DragEventArgs e)
        {
            if (_DraggedTab == null || _DraggedTabSourceProfile == null || _SelectedProfile == null)
            {
                ClearDropTarget();
                return;
            }

            Int32 targetIndex = GetTabDropIndex(e);
            await MoveDraggedTabToProfileAsync(_SelectedProfile, targetIndex).ConfigureAwait(true);
            ClearDropTarget();
            e.Handled = true;
        }

        private async Task DropProfileAsync(DragEventArgs e)
        {
            if (_DraggedProfile == null)
            {
                ClearDropTarget();
                return;
            }

            Int32 targetIndex = GetProfileDropIndex(e);
            await MoveDraggedProfileAsync(targetIndex).ConfigureAwait(true);
            ClearDropTarget();
            e.Handled = true;
        }

        private async Task DropTabOntoProfileAsync(DragEventArgs e)
        {
            if (_DraggedTab == null || _DraggedTabSourceProfile == null)
            {
                ClearDropTarget();
                return;
            }

            ApplyEditorToProfile();
            if (!(GetDropListBoxItem(e)?.DataContext is TerminalProfile targetProfile))
            {
                ClearDropTarget();
                return;
            }

            if (targetProfile == _DraggedTabSourceProfile)
            {
                ClearDropTarget();
                return;
            }

            await MoveDraggedTabToProfileAsync(targetProfile, targetProfile.Tabs.Count).ConfigureAwait(true);
            ClearDropTarget();
            e.Handled = true;
        }

        private async Task MoveDraggedProfileAsync(int targetIndex)
        {
            if (_DraggedProfile == null) return;
            ApplyEditorToProfile();

            string selectedProfileId = _DraggedProfile.Id;
            Int32 sourceIndex = _Profiles.IndexOf(_DraggedProfile);
            if (sourceIndex < 0) return;

            _Profiles.RemoveAt(sourceIndex);
            if (sourceIndex < targetIndex) targetIndex--;
            targetIndex = Math.Clamp(targetIndex, 0, _Profiles.Count);
            _Profiles.Insert(targetIndex, _DraggedProfile);

            RefreshProfiles();
            RestoreSelectedProfile(selectedProfileId);
            await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            _DropHandled = true;
        }

        private async Task MoveDraggedTabToProfileAsync(TerminalProfile? targetProfile, int targetIndex)
        {
            if (_DraggedTab == null || _DraggedTabSourceProfile == null || targetProfile == null) return;
            ApplyEditorToProfile();

            TerminalProfile sourceProfile = _DraggedTabSourceProfile;
            Int32 sourceIndex = sourceProfile.Tabs.IndexOf(_DraggedTab);
            if (sourceIndex < 0) return;

            sourceProfile.Tabs.RemoveAt(sourceIndex);
            if (sourceProfile == targetProfile && sourceIndex < targetIndex) targetIndex--;
            targetIndex = Math.Clamp(targetIndex, 0, targetProfile.Tabs.Count);
            targetProfile.Tabs.Insert(targetIndex, _DraggedTab);

            RefreshProfiles();
            RestoreSelectedProfile(targetProfile.Id);
            RefreshTabs();
            TabsList.SelectedIndex = Math.Clamp(targetIndex, 0, Math.Max(0, targetProfile.Tabs.Count - 1));
            await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            _DropHandled = true;
        }

        private void OnListDragLeave(object? sender, DragEventArgs e)
        {
            ClearDropTarget();
        }

        private void MarkDraggedControl(Control control)
        {
            ClearDragVisuals();
            _DraggedControl = control;
            control.Classes.Add(DraggingItemClass);
            control.Opacity = 0.55;
            control.Cursor = new Cursor(StandardCursorType.SizeAll);
        }

        private void MarkDropTarget(Control? control, bool isValidTarget)
        {
            if (!isValidTarget || control == null || control == _DraggedControl)
            {
                ClearDropTarget();
                return;
            }

            if (_DropTargetControl == control) return;

            ClearDropTarget();
            _DropTargetControl = control;
            control.Classes.Add(DropTargetItemClass);
            control.Opacity = 0.78;
            control.Cursor = new Cursor(StandardCursorType.SizeAll);
        }

        private void ClearDragVisuals()
        {
            if (_DraggedControl != null)
            {
                _DraggedControl.Classes.Remove(DraggingItemClass);
                _DraggedControl.Opacity = 1;
                _DraggedControl.Cursor = null;
                _DraggedControl = null;
            }

            ClearDropTarget();
        }

        private void ClearDropTarget()
        {
            if (_DropTargetControl == null) return;
            _DropTargetControl.Classes.Remove(DropTargetItemClass);
            if (_DropTargetControl != _DraggedControl) _DropTargetControl.Opacity = 1;
            _DropTargetControl.Cursor = null;
            _DropTargetControl = null;
        }

        private bool ShouldStartDrag(PointerEventArgs e, Control relativeTo)
        {
            if (_IsStartingDrag) return false;
            if (!e.GetCurrentPoint(relativeTo).Properties.IsLeftButtonPressed)
            {
                ClearPendingDrag(e);
                return false;
            }

            Point current = e.GetPosition(relativeTo);
            return
                Math.Abs(current.X - _PendingDragStartPoint.X) >= DragStartThreshold ||
                Math.Abs(current.Y - _PendingDragStartPoint.Y) >= DragStartThreshold;
        }

        private void ClearPendingDrag(PointerEventArgs? e = null)
        {
            _PendingDraggedProfile = null;
            _PendingDraggedTab = null;
            _PendingDraggedTabSourceProfile = null;
            _PendingDragItem = null;
            _PendingDragPressedEvent = null;
        }

        private void ClearLastDropTargets()
        {
            _LastProfileDropIndex = null;
            _LastProfileDropTargetProfile = null;
            _LastTabDropIndex = null;
        }

        private Int32 GetProfileDropIndex(DragEventArgs e)
        {
            ListBoxItem? item = GetDropListBoxItem(e);
            if (!(item?.DataContext is TerminalProfile profile)) return _Profiles.Count;

            Int32 index = _Profiles.IndexOf(profile);
            if (index < 0) return _Profiles.Count;
            return IsDropAfterItem(e, item) ? index + 1 : index;
        }

        private Int32 GetTabDropIndex(DragEventArgs e)
        {
            ListBoxItem? item = GetDropListBoxItem(e);
            if (_SelectedProfile == null || !(item?.DataContext is TerminalTabProfile tab)) return _SelectedProfile?.Tabs.Count ?? 0;

            Int32 index = _SelectedProfile.Tabs.IndexOf(tab);
            if (index < 0) return _SelectedProfile.Tabs.Count;
            return IsDropAfterItem(e, item) ? index + 1 : index;
        }

        private static ListBoxItem? GetDropListBoxItem(RoutedEventArgs e)
        {
            return e.Source is Control source ? GetListBoxItem(source) : null;
        }

        private static ListBoxItem? GetListBoxItem(Control source)
        {
            return source as ListBoxItem ?? source.FindAncestorOfType<ListBoxItem>();
        }

        private static async Task FlushDragVisualAsync()
        {
            await Dispatcher.UIThread.InvokeAsync(delegate { }, DispatcherPriority.Render);
        }

        private static bool IsDropAfterItem(DragEventArgs e, ListBoxItem item)
        {
            return e.GetPosition(item).Y > item.Bounds.Height / 2;
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

        private void RestoreSelectedProfile(string? profileId)
        {
            if (String.IsNullOrWhiteSpace(profileId)) return;

            Int32 index = _Profiles.FindIndex(item => item.Id == profileId);
            if (index < 0) return;
            ProfileList.SelectedIndex = index;
            _SelectedProfile = _Profiles[index];
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
