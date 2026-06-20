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
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Termrig.App.Models;
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
        private readonly ProfileFolderStore _ProfileFolderStore = new ProfileFolderStore();
        private readonly ColorSchemeStore _ColorSchemeStore = new ColorSchemeStore();
        private readonly ShellCatalog _ShellCatalog = new ShellCatalog();
        private readonly WorkspaceRecoveryStore _WorkspaceRecoveryStore = new WorkspaceRecoveryStore();
        private readonly WorkspaceRecoveryPlanner _WorkspaceRecoveryPlanner = new WorkspaceRecoveryPlanner();
        private readonly CrashLogStore _CrashLogStore = new CrashLogStore();
        private readonly string _RecoveryRunId = Guid.NewGuid().ToString("N");
        private const string RepositoryUrl = "https://github.com/jchristn/Termrig";
        private const string NoFolderLabel = "No folder";
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
        private List<ProfileFolder> _ProfileFolders = new List<ProfileFolder>();
        private List<TerminalProfile> _Profiles = new List<TerminalProfile>();
        private List<ProfileListItem> _ProfileItems = new List<ProfileListItem>();
        private TerminalProfile? _SelectedProfile = null;
        private ProfileFolder? _SelectedFolder = null;
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
        private string _LastProfileDropTargetFolderId = String.Empty;
        private TerminalProfile? _LastProfileDropTargetProfile = null;
        private int? _LastTabDropIndex = null;
        private bool _SuppressProfileSelectionChanged = false;
        private bool _IsRefreshingProfileEditor = false;
        private const string DraggingItemClass = "draggingItem";
        private const string DropTargetItemClass = "dropTargetItem";
        private const double DragStartThreshold = 4;
        private readonly List<TerminalWorkspaceWindow> _WorkspaceWindows = new List<TerminalWorkspaceWindow>();
        private readonly TaskCompletionSource<bool> _ProfilesLoaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _RecoveryRunStarted = false;
        private FileSystemWatcher? _ConfigAssetWatcher;
        private CancellationTokenSource? _ConfigAssetReloadCts;
        private readonly object _ConfigAssetReloadGate = new object();
        private const int ConfigAssetReloadDebounceMilliseconds = 300;

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
            Dispatcher.UIThread.Post(LoadProfilesAsync, DispatcherPriority.Background);
        }

        #endregion

        #region Private-Methods

        private void WireEvents()
        {
            Closed += OnMainWindowClosed;
            NewProfileButton.Click += OnNewProfileClicked;
            DeleteProfileButton.Click += OnDeleteProfileClicked;
            NewFolderButton.Click += OnNewFolderClicked;
            DeleteFolderButton.Click += OnDeleteFolderClicked;
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
            ProfileList.ContextRequested += OnProfileListContextRequested;
            TabsList.ContextRequested += OnTabsListContextRequested;
            ProfileList.SelectionChanged += OnProfileSelectionChanged;
            GlobalSchemeCombo.SelectionChanged += OnGlobalSchemeChanged;
            ProfileFolderCombo.SelectionChanged += OnProfileFolderChanged;
            AutoOpenProfileBox.IsCheckedChanged += OnAutoOpenProfileChanged;
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

        private void OnMainWindowClosed(object? sender, EventArgs e)
        {
            StopConfigAssetWatcher();
        }

        private void InitializeLists()
        {
            RefreshColorSchemeList(null);
            RefreshProfileFolderList(null);
            ProfileFontFamilyCombo.ItemsSource = _FontFamilies;
        }

        private async void LoadProfilesAsync()
        {
            try
            {
                WorkspaceRecoveryState? pendingCrashState = await GetPendingCrashStateAsync().ConfigureAwait(true);

                _ColorSchemes = await _ColorSchemeStore.LoadAsync(CancellationToken.None).ConfigureAwait(true);
                RefreshColorSchemeList(null);

                _ProfileFolders = await _ProfileFolderStore.LoadAsync(CancellationToken.None).ConfigureAwait(true);
                RefreshProfileFolderList(null);

                _Profiles = await _ProfileStore.LoadAsync(CancellationToken.None).ConfigureAwait(true);
                bool profilesChanged = ReconcileProfileFolders();
                if (!_Profiles.Any())
                {
                    _Profiles.Add(CreateDefaultProfile());
                    profilesChanged = true;
                }

                if (profilesChanged)
                {
                    await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
                }

                RefreshProfiles();
                SelectFirstProfileRow();
                bool restoreAccepted = await PromptForCrashRecoveryAsync(pendingCrashState).ConfigureAwait(true);
                await EnsureRecoveryRunStartedAsync().ConfigureAwait(true);
                if (restoreAccepted && pendingCrashState != null)
                {
                    await RestoreCrashWorkspacesAsync(pendingCrashState).ConfigureAwait(true);
                }
                else
                {
                    await OpenAutoOpenProfilesAsync().ConfigureAwait(true);
                }

                StartConfigAssetWatcher();
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
            ShellType shell = OperatingSystem.IsWindows() ? ShellType.Cmd : ShellType.Bash;
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

        private void RefreshProfiles(string? selectedProfileId = null, string? selectedFolderId = null)
        {
            if (!String.IsNullOrWhiteSpace(selectedProfileId))
            {
                EnsureProfileFolderExpanded(selectedProfileId);
            }

            _ProfileItems = BuildProfileListItems();

            _SuppressProfileSelectionChanged = true;
            try
            {
                ProfileList.ItemsSource = null;
                ProfileList.ItemsSource = _ProfileItems;
                if (!String.IsNullOrWhiteSpace(selectedProfileId))
                {
                    SelectProfileRowByProfileId(selectedProfileId);
                }
                else if (!String.IsNullOrWhiteSpace(selectedFolderId))
                {
                    SelectFolderRowByFolderId(selectedFolderId);
                }
            }
            finally
            {
                _SuppressProfileSelectionChanged = false;
            }
        }

        private void RefreshEditor()
        {
            _IsRefreshingProfileEditor = true;
            try
            {
                if (_SelectedProfile == null)
                {
                    ProfileNameBox.Text = String.Empty;
                    ProfileFolderCombo.SelectedItem = NoFolderLabel;
                    AutoOpenProfileBox.IsChecked = false;
                    TabsList.ItemsSource = null;
                    return;
                }

                ProfileNameBox.Text = _SelectedProfile.Name;
                GlobalSchemeCombo.SelectedItem = _SelectedProfile.GlobalColorScheme.Name;
                SchemeNameBox.Text = _SelectedProfile.GlobalColorScheme.Name;
                SchemeBackgroundPicker.Color = ParseColor(_SelectedProfile.GlobalColorScheme.Background);
                SchemeForegroundPicker.Color = ParseColor(_SelectedProfile.GlobalColorScheme.Foreground);
                ProfileFolderCombo.SelectedItem = GetFolderComboItem(_SelectedProfile.FolderId);
                AutoOpenProfileBox.IsChecked = _SelectedProfile.AutoOpen;
                ProfileFontFamilyCombo.SelectedItem = _SelectedProfile.FontFamily ?? "Default terminal font";
                ProfileFontSizeBox.Text = _SelectedProfile.FontSize.HasValue ? _SelectedProfile.FontSize.Value.ToString("0.##") : String.Empty;
                RefreshTabs();
            }
            finally
            {
                _IsRefreshingProfileEditor = false;
            }
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
            _SelectedProfile.FolderId = GetSelectedProfileFolderId();
            _SelectedProfile.AutoOpen = AutoOpenProfileBox.IsChecked == true;
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
            RefreshProfiles(selectedProfileId);
            RestoreSelectedProfile(selectedProfileId);
        }

        private void OnNewProfileClicked(object? sender, RoutedEventArgs e)
        {
            TerminalProfile profile = CreateDefaultProfile();
            profile.Name = "Profile " + (_Profiles.Count + 1);
            profile.FolderId = GetNewProfileFolderId();
            _Profiles.Add(profile);
            RefreshProfiles(profile.Id);
            RestoreSelectedProfile(profile.Id);
        }

        private async void OnDeleteProfileClicked(object? sender, RoutedEventArgs e)
        {
            TerminalProfile? profile = GetSelectedProfileListItem()?.Profile;
            if (profile == null) return;
            await DeleteProfileAsync(profile).ConfigureAwait(true);
        }

        private async void OnNewFolderClicked(object? sender, RoutedEventArgs e)
        {
            TextPromptWindow prompt = new TextPromptWindow("New folder", "Folder name", "New Folder");
            string? value = await prompt.ShowDialog<string?>(this).ConfigureAwait(true);
            if (value == null) return;

            string name = GetUniqueFolderName(value, null);
            if (String.IsNullOrWhiteSpace(name)) return;

            ProfileFolder folder = new ProfileFolder
            {
                Name = name
            };
            _ProfileFolders.Add(folder);
            await _ProfileFolderStore.SaveAsync(_ProfileFolders, CancellationToken.None).ConfigureAwait(true);
            RefreshProfileFolderList(folder.Name);
            RefreshProfiles(null, folder.Id);
            _SelectedFolder = folder;
            RefreshEditor();
        }

        private async void OnRenameFolderClicked(object? sender, RoutedEventArgs e)
        {
            ProfileFolder? folder = GetActiveFolder();
            if (folder == null) return;
            await RenameFolderAsync(folder).ConfigureAwait(true);
        }

        private async void OnDeleteFolderClicked(object? sender, RoutedEventArgs e)
        {
            ProfileFolder? folder = GetActiveFolder();
            if (folder == null) return;
            await DeleteFolderAsync(folder).ConfigureAwait(true);
        }

        private async void OnOpenProfileClicked(object? sender, RoutedEventArgs e)
        {
            ApplyEditorToProfile();
            if (_SelectedProfile == null) return;
            await OpenWorkspaceAsync(_SelectedProfile).ConfigureAwait(true);
        }

        private async void OnProfileListDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (!(e.Source is Control source)) return;
            ListBoxItem? item = GetListBoxItem(source);
            if (item == null) return;

            if (item.DataContext is ProfileListItem profileItem && profileItem.Folder != null)
            {
                await ToggleFolderExpansionAsync(profileItem.Folder).ConfigureAwait(true);
                e.Handled = true;
                return;
            }

            if (GetSelectedProfileListItem()?.Profile == null) return;

            ApplyEditorToProfile();
            if (_SelectedProfile == null) return;
            await OpenWorkspaceAsync(_SelectedProfile).ConfigureAwait(true);
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
            await ReloadProfilesFromStoreAsync().ConfigureAwait(true);

            TerminalProfile? profile = _Profiles.FirstOrDefault(item => item.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase));
            if (profile == null) return false;

            await OpenWorkspaceAsync(profile).ConfigureAwait(true);
            return true;
        }

        private async Task ReloadProfilesFromStoreAsync()
        {
            await ReloadConfigAssetsFromStoreAsync("Profile reload before command open failed.").ConfigureAwait(true);
        }

        private void StartConfigAssetWatcher()
        {
            if (_ConfigAssetWatcher != null)
                return;

            try
            {
                Directory.CreateDirectory(_ProfileStore.DirectoryPath);
                var watcher = new FileSystemWatcher(_ProfileStore.DirectoryPath)
                {
                    Filter = "*.json",
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
                };

                watcher.Changed += OnConfigAssetChanged;
                watcher.Created += OnConfigAssetChanged;
                watcher.Deleted += OnConfigAssetChanged;
                watcher.Renamed += OnConfigAssetRenamed;
                watcher.Error += OnConfigAssetWatcherError;
                _ConfigAssetWatcher = watcher;
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception exception)
            {
                WriteRecoveryDiagnostic("Config asset watcher start failed.", exception);
            }
        }

        private void StopConfigAssetWatcher()
        {
            lock (_ConfigAssetReloadGate)
            {
                _ConfigAssetReloadCts?.Cancel();
                _ConfigAssetReloadCts?.Dispose();
                _ConfigAssetReloadCts = null;
            }

            if (_ConfigAssetWatcher == null)
                return;

            _ConfigAssetWatcher.EnableRaisingEvents = false;
            _ConfigAssetWatcher.Dispose();
            _ConfigAssetWatcher = null;
        }

        private void OnConfigAssetChanged(object sender, FileSystemEventArgs e)
        {
            if (!IsManagedConfigAsset(e.FullPath))
                return;

            ScheduleConfigAssetReload();
        }

        private void OnConfigAssetRenamed(object sender, RenamedEventArgs e)
        {
            if (!IsManagedConfigAsset(e.FullPath) && !IsManagedConfigAsset(e.OldFullPath))
                return;

            ScheduleConfigAssetReload();
        }

        private void OnConfigAssetWatcherError(object sender, ErrorEventArgs e)
        {
            WriteRecoveryDiagnostic("Config asset watcher error.", e.GetException());
        }

        private bool IsManagedConfigAsset(string path)
        {
            string fileName = Path.GetFileName(path);
            return fileName.Equals(Path.GetFileName(_ProfileStore.FilePath), StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals(Path.GetFileName(_ProfileFolderStore.FilePath), StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals(Path.GetFileName(_ColorSchemeStore.FilePath), StringComparison.OrdinalIgnoreCase);
        }

        private void ScheduleConfigAssetReload()
        {
            CancellationTokenSource cts;
            lock (_ConfigAssetReloadGate)
            {
                _ConfigAssetReloadCts?.Cancel();
                _ConfigAssetReloadCts?.Dispose();
                _ConfigAssetReloadCts = new CancellationTokenSource();
                cts = _ConfigAssetReloadCts;
            }

            _ = ReloadConfigAssetsAfterDelayAsync(cts.Token);
        }

        private async Task ReloadConfigAssetsAfterDelayAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(ConfigAssetReloadDebounceMilliseconds, token).ConfigureAwait(false);
                Dispatcher.UIThread.Post(
                    async () => await ReloadConfigAssetsFromStoreAsync("Config asset reload failed.").ConfigureAwait(true),
                    DispatcherPriority.Background);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task ReloadConfigAssetsFromStoreAsync(string failureSummary)
        {
            string? selectedProfileId = _SelectedProfile?.Id;
            string? selectedFolderId = _SelectedFolder?.Id;
            string? selectedSchemeName = GlobalSchemeCombo.SelectedItem as string;

            try
            {
                _ColorSchemes = await LoadConfigAssetWithRetryAsync(_ColorSchemeStore.LoadAsync, CancellationToken.None).ConfigureAwait(true);
                _ProfileFolders = await LoadConfigAssetWithRetryAsync(_ProfileFolderStore.LoadAsync, CancellationToken.None).ConfigureAwait(true);
                _Profiles = await LoadConfigAssetWithRetryAsync(_ProfileStore.LoadAsync, CancellationToken.None).ConfigureAwait(true);

                bool profilesChanged = ReconcileProfileFolders();
                if (!_Profiles.Any())
                {
                    _Profiles.Add(CreateDefaultProfile());
                    profilesChanged = true;
                }

                if (profilesChanged)
                {
                    await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
                }

                RefreshColorSchemeList(selectedSchemeName);
                string? selectedFolderName = _ProfileFolders.FirstOrDefault(item => item.Id == selectedFolderId)?.Name;
                RefreshProfileFolderList(selectedFolderName);
                RefreshProfiles(selectedProfileId, selectedFolderId);
                if (!String.IsNullOrWhiteSpace(selectedProfileId))
                {
                    RestoreSelectedProfile(selectedProfileId);
                }
                else if (!String.IsNullOrWhiteSpace(selectedFolderId))
                {
                    SelectFolderRowByFolderId(selectedFolderId);
                    RefreshEditor();
                }
                else
                {
                    RestoreSelectedProfile(null);
                }
            }
            catch (Exception exception)
            {
                WriteRecoveryDiagnostic(failureSummary, exception);
            }
        }

        private static async Task<T> LoadConfigAssetWithRetryAsync<T>(Func<CancellationToken, Task<T>> loadAsync, CancellationToken token)
        {
            const int attempts = 4;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await loadAsync(token).ConfigureAwait(false);
                }
                catch (Exception exception) when (attempt < attempts && IsTransientConfigLoadException(exception))
                {
                    await Task.Delay(150, token).ConfigureAwait(false);
                }
            }
        }

        private static bool IsTransientConfigLoadException(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is JsonException;
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

        /// <summary>
        /// Mark the current Termrig run as a clean shutdown for workspace recovery.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task representing the clean shutdown update.</returns>
        public async Task MarkCleanShutdownAsync(CancellationToken token = default)
        {
            if (!_RecoveryRunStarted) return;
            await _WorkspaceRecoveryStore.MarkCleanShutdownAsync(_RecoveryRunId, token).ConfigureAwait(false);
        }

        private async Task<WorkspaceRecoveryState?> GetPendingCrashStateAsync()
        {
            try
            {
                return await _WorkspaceRecoveryStore.GetPendingCrashAsync(CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                WriteRecoveryDiagnostic("Workspace recovery state load failed.", exception);
                return null;
            }
        }

        private async Task<bool> PromptForCrashRecoveryAsync(WorkspaceRecoveryState? pendingCrashState)
        {
            if (pendingCrashState == null) return false;

            CrashRecoveryPromptWindow prompt = new CrashRecoveryPromptWindow(pendingCrashState.OpenWorkspaces.Count);
            bool restoreAccepted = await prompt.ShowDialog<bool>(this).ConfigureAwait(true);

            try
            {
                await _WorkspaceRecoveryStore.MarkRestorePromptHandledAsync(CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                WriteRecoveryDiagnostic("Workspace recovery prompt state update failed.", exception);
            }

            return restoreAccepted;
        }

        private async Task RestoreCrashWorkspacesAsync(WorkspaceRecoveryState pendingCrashState)
        {
            WorkspaceRecoveryRestorePlan plan = _WorkspaceRecoveryPlanner.BuildRestorePlan(pendingCrashState, _Profiles);
            foreach (WorkspaceRecoveryRestoreAction action in plan.RestoreActions)
            {
                await OpenWorkspaceAsync(action.Profile).ConfigureAwait(true);
            }

            if (plan.SkippedWorkspaces.Count > 0)
            {
                WriteRecoveryDiagnostic(
                    "Workspace recovery skipped profiles.",
                    new InvalidOperationException("Skipped " + plan.SkippedWorkspaces.Count + " workspace recovery entries because their profiles were missing or ambiguous."));
            }
        }

        private async Task EnsureRecoveryRunStartedAsync()
        {
            if (_RecoveryRunStarted) return;

            try
            {
                await _WorkspaceRecoveryStore.MarkRunStartedAsync(_RecoveryRunId, Environment.ProcessId, CancellationToken.None).ConfigureAwait(true);
                _RecoveryRunStarted = true;
            }
            catch (Exception exception)
            {
                WriteRecoveryDiagnostic("Workspace recovery run start failed.", exception);
            }
        }

        private async Task OpenWorkspaceAsync(TerminalProfile profile)
        {
            string workspaceId = Guid.NewGuid().ToString("N");
            TerminalWorkspaceWindow window = new TerminalWorkspaceWindow(profile, _ProfileStore, _ShellCatalog, _ColorSchemes, workspaceId);
            _WorkspaceWindows.Add(window);
            window.Closed += async delegate
            {
                _WorkspaceWindows.Remove(window);
                await RegisterWorkspaceClosedAsync(window.WorkspaceId).ConfigureAwait(true);
            };
            await RegisterWorkspaceOpenedAsync(window).ConfigureAwait(true);
            window.Show();
        }

        private async Task RegisterWorkspaceOpenedAsync(TerminalWorkspaceWindow window)
        {
            try
            {
                await EnsureRecoveryRunStartedAsync().ConfigureAwait(true);
                if (!_RecoveryRunStarted) return;

                WorkspaceRecoveryWorkspace workspace = new WorkspaceRecoveryWorkspace
                {
                    WorkspaceId = window.WorkspaceId,
                    ProfileId = window.ProfileId,
                    ProfileName = window.ProfileName,
                    OpenedUtc = DateTime.UtcNow
                };
                await _WorkspaceRecoveryStore.RegisterWorkspaceOpenedAsync(_RecoveryRunId, workspace, CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                WriteRecoveryDiagnostic("Workspace recovery open update failed.", exception);
            }
        }

        private async Task RegisterWorkspaceClosedAsync(string workspaceId)
        {
            if (!_RecoveryRunStarted) return;

            try
            {
                await _WorkspaceRecoveryStore.RegisterWorkspaceClosedAsync(_RecoveryRunId, workspaceId, CancellationToken.None).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                WriteRecoveryDiagnostic("Workspace recovery close update failed.", exception);
            }
        }

        private void WriteRecoveryDiagnostic(string summary, Exception exception)
        {
            try
            {
                _CrashLogStore.Write("Termrig", "application", summary, exception.ToString());
            }
            catch
            {
            }
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

        private async void OnDeleteTabClicked(object? sender, RoutedEventArgs e)
        {
            if (_SelectedProfile == null) return;
            Int32 index = TabsList.SelectedIndex;
            await DeleteTabAsync(index).ConfigureAwait(true);
        }

        private void OnProfileListContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (!(e.Source is Control source)) return;
            ListBoxItem? item = GetListBoxItem(source);
            if (!(item?.DataContext is ProfileListItem profileItem)) return;

            SelectProfileListItemForContext(profileItem);
            ContextMenu? menu = profileItem.Profile != null
                ? BuildProfileContextMenu(profileItem.Profile)
                : profileItem.Folder != null
                    ? BuildFolderContextMenu(profileItem.Folder)
                    : null;
            if (menu == null) return;

            menu.Open(item);
            e.Handled = true;
        }

        private void OnTabsListContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            if (_SelectedProfile == null) return;
            if (!(e.Source is Control source)) return;
            ListBoxItem? item = GetListBoxItem(source);
            if (!(item?.DataContext is TerminalTabProfile tab)) return;

            Int32 index = _SelectedProfile.Tabs.IndexOf(tab);
            if (index < 0) return;

            TabsList.SelectedIndex = index;
            BuildTabContextMenu(tab).Open(item);
            e.Handled = true;
        }

        private async void OnProfileFolderTogglePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(ProfileList).Properties.IsLeftButtonPressed) return;
            if (!((sender as Control)?.DataContext is ProfileListItem item) || item.Folder == null) return;

            e.Handled = true;
            await ToggleFolderExpansionAsync(item.Folder).ConfigureAwait(true);
        }

        private ContextMenu BuildProfileContextMenu(TerminalProfile profile)
        {
            return new ContextMenu
            {
                ItemsSource = new MenuItem[]
                {
                    CreateAsyncMenuItem("Open", async delegate { await OpenProfileFromContextAsync(profile).ConfigureAwait(true); }),
                    CreateAsyncMenuItem("Rename", async delegate { await RenameProfileAsync(profile).ConfigureAwait(true); }),
                    CreateAsyncMenuItem("Delete", async delegate { await DeleteProfileAsync(profile).ConfigureAwait(true); })
                }
            };
        }

        private ContextMenu BuildFolderContextMenu(ProfileFolder folder)
        {
            return new ContextMenu
            {
                ItemsSource = new MenuItem[]
                {
                    CreateAsyncMenuItem("Rename", async delegate { await RenameFolderAsync(folder).ConfigureAwait(true); }),
                    CreateAsyncMenuItem("Delete", async delegate { await DeleteFolderAsync(folder).ConfigureAwait(true); })
                }
            };
        }

        private ContextMenu BuildTabContextMenu(TerminalTabProfile tab)
        {
            return new ContextMenu
            {
                ItemsSource = new MenuItem[]
                {
                    CreateAsyncMenuItem("Rename", async delegate { await RenameTabAsync(tab).ConfigureAwait(true); }),
                    CreateAsyncMenuItem("Edit", async delegate { await EditSelectedTabAsync().ConfigureAwait(true); }),
                    CreateAsyncMenuItem("Delete", async delegate
                    {
                        Int32 index = _SelectedProfile?.Tabs.IndexOf(tab) ?? -1;
                        await DeleteTabAsync(index).ConfigureAwait(true);
                    })
                }
            };
        }

        private static MenuItem CreateMenuItem(string header, Action onClick)
        {
            MenuItem item = new MenuItem { Header = header };
            item.Click += delegate { onClick(); };
            return item;
        }

        private static MenuItem CreateAsyncMenuItem(string header, Func<Task> onClick)
        {
            MenuItem item = new MenuItem { Header = header };
            item.Click += async delegate { await onClick().ConfigureAwait(true); };
            return item;
        }

        private async Task OpenProfileFromContextAsync(TerminalProfile profile)
        {
            if (_SelectedProfile == profile)
            {
                ApplyEditorToProfile();
            }

            await OpenWorkspaceAsync(profile).ConfigureAwait(true);
        }

        private async Task RenameProfileAsync(TerminalProfile profile)
        {
            TextPromptWindow prompt = new TextPromptWindow("Rename profile", "Profile name", profile.Name);
            string? value = await prompt.ShowDialog<string?>(this).ConfigureAwait(true);
            if (String.IsNullOrWhiteSpace(value)) return;

            profile.Name = value.Trim();
            await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            RefreshProfiles(profile.Id);
            RestoreSelectedProfile(profile.Id);
        }

        private async Task DeleteProfileAsync(TerminalProfile profile)
        {
            Int32 index = _Profiles.IndexOf(profile);
            if (index < 0) return;
            bool confirmed = await ConfirmDeleteProfileAsync(profile).ConfigureAwait(true);
            if (!confirmed) return;

            _Profiles.RemoveAt(index);
            await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            RefreshProfiles();
            TerminalProfile? nextProfile = _Profiles.Count > 0 ? _Profiles[Math.Min(index, _Profiles.Count - 1)] : null;
            RestoreSelectedProfile(nextProfile?.Id);
        }

        private async Task RenameFolderAsync(ProfileFolder folder)
        {
            TextPromptWindow prompt = new TextPromptWindow("Rename folder", "Folder name", folder.Name);
            string? value = await prompt.ShowDialog<string?>(this).ConfigureAwait(true);
            if (String.IsNullOrWhiteSpace(value)) return;

            folder.Name = GetUniqueFolderName(value, folder.Id);
            await _ProfileFolderStore.SaveAsync(_ProfileFolders, CancellationToken.None).ConfigureAwait(true);
            RefreshProfileFolderList(folder.Name);
            RefreshProfiles(null, folder.Id);
        }

        private async Task DeleteFolderAsync(ProfileFolder folder)
        {
            bool confirmed = await ConfirmDeleteFolderAsync(folder).ConfigureAwait(true);
            if (!confirmed) return;

            _ProfileFolders.Remove(folder);
            foreach (TerminalProfile profile in _Profiles.Where(item => item.FolderId == folder.Id))
            {
                profile.FolderId = String.Empty;
            }

            await _ProfileFolderStore.SaveAsync(_ProfileFolders, CancellationToken.None).ConfigureAwait(true);
            await _ProfileStore.SaveAsync(_Profiles, CancellationToken.None).ConfigureAwait(true);
            RefreshProfileFolderList(null);
            RefreshProfiles();
            SelectFirstProfileRow();
        }

        private async Task RenameTabAsync(TerminalTabProfile tab)
        {
            if (_SelectedProfile == null) return;
            Int32 index = _SelectedProfile.Tabs.IndexOf(tab);
            if (index < 0) return;

            TextPromptWindow prompt = new TextPromptWindow("Rename tab", "Tab name", tab.Name);
            string? value = await prompt.ShowDialog<string?>(this).ConfigureAwait(true);
            if (String.IsNullOrWhiteSpace(value)) return;

            tab.Name = value.Trim();
            RefreshTabs();
            TabsList.SelectedIndex = index;
        }

        private async Task DeleteTabAsync(Int32 index)
        {
            if (_SelectedProfile == null) return;
            if (index < 0 || index >= _SelectedProfile.Tabs.Count) return;

            TerminalTabProfile tab = _SelectedProfile.Tabs[index];
            bool confirmed = await ConfirmDeleteTabAsync(_SelectedProfile, tab).ConfigureAwait(true);
            if (!confirmed) return;

            _SelectedProfile.Tabs.RemoveAt(index);
            RefreshTabs();
            TabsList.SelectedIndex = Math.Min(index, _SelectedProfile.Tabs.Count - 1);
        }

        private async Task<bool> ConfirmDeleteProfileAsync(TerminalProfile profile)
        {
            DeleteConfirmationWindow confirmation = new DeleteConfirmationWindow(
                "Delete profile",
                "Delete profile?",
                "This will delete the saved profile \"" + profile.Name + "\" and its " + BuildTabCountText(profile.Tabs.Count) + ".",
                "Delete profile");
            return await confirmation.ShowDialog<bool>(this).ConfigureAwait(true);
        }

        private async Task<bool> ConfirmDeleteTabAsync(TerminalProfile profile, TerminalTabProfile tab)
        {
            DeleteConfirmationWindow confirmation = new DeleteConfirmationWindow(
                "Delete tab",
                "Delete tab?",
                "This will remove the tab \"" + tab.Name + "\" from profile \"" + profile.Name + "\".",
                "Delete tab");
            return await confirmation.ShowDialog<bool>(this).ConfigureAwait(true);
        }

        private static string BuildTabCountText(int tabCount)
        {
            return tabCount == 1 ? "1 tab" : tabCount + " tabs";
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
            if (_SuppressProfileSelectionChanged) return;

            ProfileListItem? item = GetSelectedProfileListItem();
            if (item?.Folder != null)
            {
                _SelectedProfile = null;
                _SelectedFolder = item.Folder;
                RefreshEditor();
                return;
            }

            _SelectedFolder = null;
            _SelectedProfile = item?.Profile;
            RefreshEditor();
        }

        private void OnGlobalSchemeChanged(object? sender, SelectionChangedEventArgs e)
        {
            ApplySelectedGlobalScheme();
        }

        private void OnProfileFolderChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_IsRefreshingProfileEditor || _SelectedProfile == null) return;

            ApplyEditorToProfile();
            RefreshProfiles(_SelectedProfile.Id);
        }

        private void OnAutoOpenProfileChanged(object? sender, RoutedEventArgs e)
        {
            if (_IsRefreshingProfileEditor || _SelectedProfile == null) return;

            ApplyEditorToProfile();
            RefreshProfiles(_SelectedProfile.Id);
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
            if (!((item?.DataContext as ProfileListItem)?.Profile is TerminalProfile profile)) return;

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
                    await MoveDraggedProfileAsync(_LastProfileDropIndex.Value, _LastProfileDropTargetFolderId).ConfigureAwait(true);
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
                _LastProfileDropTargetFolderId = GetProfileDropFolderId(e);
                _LastProfileDropTargetProfile = null;
                _LastTabDropIndex = null;
            }
            else if (_DraggedTab != null)
            {
                _LastProfileDropIndex = null;
                _LastProfileDropTargetFolderId = String.Empty;
                _LastProfileDropTargetProfile = GetProfileFromListBoxItem(GetDropListBoxItem(e));
                e.DragEffects = _LastProfileDropTargetProfile == null ? DragDropEffects.None : DragDropEffects.Move;
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
            await MoveDraggedProfileAsync(targetIndex, GetProfileDropFolderId(e)).ConfigureAwait(true);
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
            TerminalProfile? targetProfile = GetProfileFromListBoxItem(GetDropListBoxItem(e));
            if (targetProfile == null)
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

        private async Task MoveDraggedProfileAsync(int targetIndex, string targetFolderId)
        {
            if (_DraggedProfile == null) return;
            ApplyEditorToProfile();

            string selectedProfileId = _DraggedProfile.Id;
            Int32 sourceIndex = _Profiles.IndexOf(_DraggedProfile);
            if (sourceIndex < 0) return;

            _Profiles.RemoveAt(sourceIndex);
            if (sourceIndex < targetIndex) targetIndex--;
            targetIndex = Math.Clamp(targetIndex, 0, _Profiles.Count);
            _DraggedProfile.FolderId = targetFolderId ?? String.Empty;
            _Profiles.Insert(targetIndex, _DraggedProfile);

            RefreshProfiles(selectedProfileId);
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

            RefreshProfiles(targetProfile.Id);
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
            _LastProfileDropTargetFolderId = String.Empty;
            _LastProfileDropTargetProfile = null;
            _LastTabDropIndex = null;
        }

        private Int32 GetProfileDropIndex(DragEventArgs e)
        {
            ListBoxItem? item = GetDropListBoxItem(e);
            ProfileListItem? profileItem = item?.DataContext as ProfileListItem;
            if (profileItem?.Folder != null) return GetProfileInsertIndexForFolder(profileItem.Folder.Id);
            if (item == null || !(profileItem?.Profile is TerminalProfile profile)) return _Profiles.Count;

            Int32 index = _Profiles.IndexOf(profile);
            if (index < 0) return _Profiles.Count;
            return IsDropAfterItem(e, item) ? index + 1 : index;
        }

        private string GetProfileDropFolderId(DragEventArgs e)
        {
            ListBoxItem? item = GetDropListBoxItem(e);
            ProfileListItem? profileItem = item?.DataContext as ProfileListItem;
            if (profileItem?.Folder != null) return profileItem.Folder.Id;
            if (profileItem?.Profile != null) return profileItem.Profile.FolderId ?? String.Empty;
            return String.Empty;
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

        private List<ProfileListItem> BuildProfileListItems()
        {
            List<ProfileListItem> items = new List<ProfileListItem>();

            foreach (TerminalProfile profile in _Profiles.Where(item => String.IsNullOrWhiteSpace(item.FolderId)))
            {
                items.Add(new ProfileListItem { Profile = profile });
            }

            foreach (ProfileFolder folder in _ProfileFolders)
            {
                items.Add(new ProfileListItem { Folder = folder });
                if (!folder.IsExpanded) continue;

                foreach (TerminalProfile profile in _Profiles.Where(item => item.FolderId == folder.Id))
                {
                    items.Add(new ProfileListItem { Profile = profile });
                }
            }

            return items;
        }

        private void SelectFirstProfileRow()
        {
            TerminalProfile? firstProfile = _Profiles.FirstOrDefault();
            RestoreSelectedProfile(firstProfile?.Id);
        }

        private void EnsureProfileFolderExpanded(string profileId)
        {
            TerminalProfile? profile = _Profiles.FirstOrDefault(item => item.Id == profileId);
            ProfileFolder? folder = FindFolderById(profile?.FolderId);
            if (folder == null || folder.IsExpanded) return;

            folder.IsExpanded = true;
            _ = _ProfileFolderStore.SaveAsync(_ProfileFolders, CancellationToken.None);
        }

        private void SelectProfileRowByProfileId(string profileId)
        {
            Int32 index = _ProfileItems.FindIndex(item => item.Profile?.Id == profileId);
            ProfileList.SelectedIndex = index;
            if (index >= 0)
            {
                _SelectedProfile = _ProfileItems[index].Profile;
                _SelectedFolder = null;
            }
        }

        private void SelectFolderRowByFolderId(string folderId)
        {
            Int32 index = _ProfileItems.FindIndex(item => item.Folder?.Id == folderId);
            ProfileList.SelectedIndex = index;
            if (index >= 0)
            {
                _SelectedProfile = null;
                _SelectedFolder = _ProfileItems[index].Folder;
            }
        }

        private ProfileListItem? GetSelectedProfileListItem()
        {
            return ProfileList.SelectedItem as ProfileListItem;
        }

        private void SelectProfileListItemForContext(ProfileListItem item)
        {
            _SuppressProfileSelectionChanged = true;
            try
            {
                ProfileList.SelectedItem = item;
            }
            finally
            {
                _SuppressProfileSelectionChanged = false;
            }

            _SelectedProfile = item.Profile;
            _SelectedFolder = item.Folder;
            RefreshEditor();
        }

        private async Task ToggleFolderExpansionAsync(ProfileFolder folder)
        {
            folder.IsExpanded = !folder.IsExpanded;
            await _ProfileFolderStore.SaveAsync(_ProfileFolders, CancellationToken.None).ConfigureAwait(true);
            RefreshProfiles(null, folder.Id);
            RefreshEditor();
        }

        private static TerminalProfile? GetProfileFromListBoxItem(ListBoxItem? item)
        {
            return (item?.DataContext as ProfileListItem)?.Profile;
        }

        private int GetProfileInsertIndexForFolder(string folderId)
        {
            Int32 lastIndex = _Profiles.FindLastIndex(item => item.FolderId == folderId);
            return lastIndex >= 0 ? lastIndex + 1 : _Profiles.Count;
        }

        private void RefreshProfileFolderList(string? selectedName)
        {
            List<string> folders = new List<string> { NoFolderLabel };
            folders.AddRange(_ProfileFolders.Select(item => item.Name));
            ProfileFolderCombo.ItemsSource = folders;
            if (!String.IsNullOrWhiteSpace(selectedName) && folders.Any(item => item.Equals(selectedName, StringComparison.OrdinalIgnoreCase)))
            {
                ProfileFolderCombo.SelectedItem = folders.First(item => item.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
            }
        }

        private string GetFolderComboItem(string? folderId)
        {
            if (String.IsNullOrWhiteSpace(folderId)) return NoFolderLabel;

            ProfileFolder? folder = FindFolderById(folderId);
            return folder == null ? NoFolderLabel : folder.Name;
        }

        private string GetSelectedProfileFolderId()
        {
            if (!(ProfileFolderCombo.SelectedItem is string selectedFolder) || selectedFolder == NoFolderLabel) return String.Empty;

            ProfileFolder? folder = _ProfileFolders.FirstOrDefault(item => item.Name.Equals(selectedFolder, StringComparison.OrdinalIgnoreCase));
            return folder?.Id ?? String.Empty;
        }

        private string GetNewProfileFolderId()
        {
            if (_SelectedFolder != null) return _SelectedFolder.Id;
            if (!String.IsNullOrWhiteSpace(_SelectedProfile?.FolderId)) return _SelectedProfile.FolderId;
            return String.Empty;
        }

        private ProfileFolder? GetActiveFolder()
        {
            ProfileListItem? selectedItem = GetSelectedProfileListItem();
            if (selectedItem?.Folder != null) return selectedItem.Folder;
            if (!String.IsNullOrWhiteSpace(_SelectedProfile?.FolderId)) return FindFolderById(_SelectedProfile.FolderId);
            return _SelectedFolder;
        }

        private ProfileFolder? FindFolderById(string? folderId)
        {
            if (String.IsNullOrWhiteSpace(folderId)) return null;
            return _ProfileFolders.FirstOrDefault(item => item.Id == folderId);
        }

        private bool ReconcileProfileFolders()
        {
            HashSet<string> folderIds = _ProfileFolders.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool changed = false;
            foreach (TerminalProfile profile in _Profiles)
            {
                if (profile.FolderId == null)
                {
                    profile.FolderId = String.Empty;
                    changed = true;
                    continue;
                }

                if (!String.IsNullOrWhiteSpace(profile.FolderId) && !folderIds.Contains(profile.FolderId))
                {
                    profile.FolderId = String.Empty;
                    changed = true;
                }
            }

            return changed;
        }

        private async Task OpenAutoOpenProfilesAsync()
        {
            foreach (TerminalProfile profile in _Profiles.Where(item => item.AutoOpen))
            {
                await OpenWorkspaceAsync(profile).ConfigureAwait(true);
            }
        }

        private async Task<bool> ConfirmDeleteFolderAsync(ProfileFolder folder)
        {
            Int32 profileCount = _Profiles.Count(item => item.FolderId == folder.Id);
            DeleteConfirmationWindow confirmation = new DeleteConfirmationWindow(
                "Remove folder",
                "Remove folder?",
                "This will remove the folder \"" + folder.Name + "\" and move its " + BuildProfileCountText(profileCount) + " to No folder.",
                "Remove folder");
            return await confirmation.ShowDialog<bool>(this).ConfigureAwait(true);
        }

        private string GetUniqueFolderName(string requestedName, string? editingFolderId)
        {
            string baseName = String.IsNullOrWhiteSpace(requestedName) ? "New Folder" : requestedName.Trim();
            string candidate = baseName;
            Int32 suffix = 2;
            while (_ProfileFolders.Any(item =>
                item.Id != editingFolderId &&
                item.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = baseName + " " + suffix;
                suffix++;
            }

            return candidate;
        }

        private static string BuildProfileCountText(int profileCount)
        {
            return profileCount == 1 ? "1 profile" : profileCount + " profiles";
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
            if (String.IsNullOrWhiteSpace(profileId))
            {
                _SuppressProfileSelectionChanged = true;
                try
                {
                    ProfileList.SelectedIndex = -1;
                }
                finally
                {
                    _SuppressProfileSelectionChanged = false;
                }

                _SelectedProfile = null;
                _SelectedFolder = null;
                RefreshEditor();
                return;
            }

            TerminalProfile? profile = _Profiles.FirstOrDefault(item => item.Id == profileId);
            if (profile == null)
            {
                RestoreSelectedProfile(null);
                return;
            }

            ProfileFolder? folder = FindFolderById(profile.FolderId);
            if (folder != null && !folder.IsExpanded)
            {
                folder.IsExpanded = true;
                _ = _ProfileFolderStore.SaveAsync(_ProfileFolders, CancellationToken.None);
                RefreshProfiles(profile.Id);
                RefreshEditor();
                return;
            }

            _SuppressProfileSelectionChanged = true;
            try
            {
                SelectProfileRowByProfileId(profileId);
            }
            finally
            {
                _SuppressProfileSelectionChanged = false;
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
