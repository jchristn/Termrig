namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Input;
    using Avalonia.Media;
    using Avalonia.Threading;
    using Avalonia.VisualTree;
    using Iciclecreek.Terminal;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Termrig.App.Models;
    using Termrig.Core;
    using Termrig.Core.Models;
    using Termrig.Core.Services;

    /// <summary>
    /// Tabbed terminal workspace window.
    /// </summary>
    public partial class TerminalWorkspaceWindow : Window
    {
        #region Private-Members

        private readonly TerminalProfile _Profile;
        private readonly ProfileStore _ProfileStore;
        private readonly ShellCatalog _ShellCatalog;
        private readonly CrashLogStore _CrashLogStore = new CrashLogStore();
        private readonly List<ColorScheme> _ColorSchemes;
        private readonly List<TerminalSession> _Sessions = new List<TerminalSession>();
        private TerminalSession? _SelectedSession = null;
        private TerminalSession? _DraggedSession = null;
        private Control? _DropTargetHeader = null;
        private bool _IsClosingWorkspace = false;
        private bool _IsCloseConfirmationOpen = false;
        private bool _HasConfirmedWorkspaceClose = false;
        private bool _SuppressNextShortcutTextInput = false;
        private const double TerminalFontZoomStep = 1;
        private const double MinimumTerminalFontSize = 8;
        private const double MaximumTerminalFontSize = 36;
        private const string AttentionIndicatorTag = "TerminalAttentionIndicator";

        #endregion

        #region Public-Members

        /// <summary>
        /// Profile name for this workspace.
        /// </summary>
        public string ProfileName
        {
            get
            {
                return _Profile.Name;
            }
        }

        /// <summary>
        /// Profile identifier for this workspace.
        /// </summary>
        public string ProfileId
        {
            get
            {
                return _Profile.Id;
            }
        }

        /// <summary>
        /// Runtime workspace instance identifier.
        /// </summary>
        public string WorkspaceId { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the terminal workspace window for the XAML loader.
        /// </summary>
        public TerminalWorkspaceWindow()
            : this(new TerminalProfile { Name = "Default" }, new ProfileStore(), new ShellCatalog(), ColorSchemeCatalog.GetSchemes())
        {
        }

        /// <summary>
        /// Instantiate the terminal workspace window.
        /// </summary>
        /// <param name="profile">Profile to open.</param>
        /// <param name="profileStore">Profile store used for save operations.</param>
        /// <param name="shellCatalog">Shell catalog.</param>
        /// <param name="colorSchemes">Available color schemes.</param>
        /// <exception cref="ArgumentNullException">Thrown when required inputs are null.</exception>
        public TerminalWorkspaceWindow(TerminalProfile profile, ProfileStore profileStore, ShellCatalog shellCatalog, List<ColorScheme> colorSchemes)
            : this(profile, profileStore, shellCatalog, colorSchemes, Guid.NewGuid().ToString("N"))
        {
        }

        /// <summary>
        /// Instantiate the terminal workspace window.
        /// </summary>
        /// <param name="profile">Profile to open.</param>
        /// <param name="profileStore">Profile store used for save operations.</param>
        /// <param name="shellCatalog">Shell catalog.</param>
        /// <param name="colorSchemes">Available color schemes.</param>
        /// <param name="workspaceId">Runtime workspace instance identifier.</param>
        /// <exception cref="ArgumentNullException">Thrown when required inputs are null.</exception>
        public TerminalWorkspaceWindow(TerminalProfile profile, ProfileStore profileStore, ShellCatalog shellCatalog, List<ColorScheme> colorSchemes, string workspaceId)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(profileStore);
            ArgumentNullException.ThrowIfNull(shellCatalog);
            ArgumentNullException.ThrowIfNull(colorSchemes);
            if (String.IsNullOrWhiteSpace(workspaceId)) throw new ArgumentNullException(nameof(workspaceId));

            _Profile = profile;
            _ProfileStore = profileStore;
            _ShellCatalog = shellCatalog;
            _ColorSchemes = colorSchemes;
            WorkspaceId = workspaceId;

            InitializeComponent();
            Title = profile.Name + " | Termrig Workspace";
            TitleText.Text = profile.Name;
            WireEvents();
            LaunchProfile();
        }

        #endregion

        #region Private-Methods

        private void WireEvents()
        {
            AddTabButton.Click += OnAddTabClicked;
            EditTabButton.Click += OnEditTabClicked;
            SaveProfileButton.Click += OnSaveProfileClicked;
            AddHandler(KeyDownEvent, OnWindowPreviewKeyDown, RoutingStrategies.Tunnel, true);
            AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Bubble, true);
            AddHandler(TextInputEvent, OnWindowTextInput, RoutingStrategies.Tunnel, true);
            Opened += OnWorkspaceOpened;
            Activated += OnWorkspaceActivated;
            Closing += OnWindowClosing;
            DragDrop.SetAllowDrop(TerminalTabHeaders, true);
            DragDrop.AddDragOverHandler(TerminalTabHeaders, OnTabHeadersDragOver);
            DragDrop.AddDropHandler(TerminalTabHeaders, OnTabHeadersDrop);
            DragDrop.AddDragLeaveHandler(TerminalTabHeaders, OnTabHeadersDragLeave);
        }

        private void LaunchProfile()
        {
            bool isFirstTab = true;
            foreach (TerminalTabProfile tab in _Profile.Tabs)
            {
                AddTerminalTab(tab, true, isFirstTab);
                isFirstTab = false;
            }
        }

        private void OnWorkspaceOpened(object? sender, EventArgs e)
        {
            _ = LaunchAllSessionsAsync();
        }

        private void OnWorkspaceActivated(object? sender, EventArgs e)
        {
            ClearAttention(GetSelectedSession());
        }

        private async Task LaunchAllSessionsAsync()
        {
            foreach (TerminalSession session in _Sessions.ToList())
            {
                await LaunchTerminalAsync(session, session.LaunchPlan).ConfigureAwait(true);
            }

            FocusSelectedTerminal();
        }

        private void AddTerminalTab(TerminalTabProfile tab, bool isProfileMember, bool selectTab = true)
        {
            ShellLaunchPlan plan = _ShellCatalog.BuildLaunchPlan(tab);
            ColorScheme scheme = tab.ColorSchemeOverride ?? _Profile.GlobalColorScheme;

            TerminalControl terminal = new TerminalControl
            {
                BufferSize = ResolveTerminalBufferSize(tab),
                Background = Brush.Parse(scheme.Background),
                Foreground = Brush.Parse(scheme.Foreground),
                Focusable = true,
                IsHitTestVisible = false,
                Opacity = 0,
                Process = String.Empty,
                Options = BuildTerminalOptions(tab)
            };
            ApplyTerminalAppearance(terminal, _Profile, tab, scheme);

            TerminalSession session = new TerminalSession
            {
                TabProfile = tab,
                Terminal = terminal,
                IsProfileMember = isProfileMember,
                LaunchPlan = plan,
                RuntimeDefaultFontSize = terminal.FontSize
            };

            session.Header = BuildTabHeader(session);
            WireTerminalSessionEvents(session);
            _Sessions.Add(session);
            TerminalTabHeaders.Children.Add(session.Header);
            TerminalSurface.Children.Add(terminal);
            if (selectTab || _SelectedSession == null)
            {
                SelectSession(session);
            }
        }

        private async Task LaunchTerminalAsync(TerminalSession session, ShellLaunchPlan plan)
        {
            if (session.IsLaunchRequested) return;
            session.IsLaunchRequested = true;

            try
            {
                await session.Terminal.LaunchProcess(plan.StartingDirectory, plan.Executable, plan.Arguments.ToArray()).ConfigureAwait(true);
                if (session.IsClosingByTermrig) return;
                await RunStartupCommandsAsync(session, plan.StartupCommands).ConfigureAwait(true);
                if (_SelectedSession == session)
                {
                    FocusTerminal(session);
                }
            }
            catch (Exception exception)
            {
                bool wasSelected = _SelectedSession == session;
                TerminalTabHeaders.Children.Remove(session.Header);
                TerminalSurface.Children.Remove(session.Terminal);
                _Sessions.Remove(session);
                UnwireTerminalSessionEvents(session);
                if (wasSelected)
                {
                    _SelectedSession = null;
                    if (_Sessions.Count > 0) SelectSession(_Sessions[0]);
                }

                WriteTerminalCrashLog(session.TabProfile, "Terminal launch failed.", BuildLaunchFailureDetails(session.TabProfile, plan, exception));
                ShowTerminalLaunchError(session.TabProfile, plan, exception);
            }
        }

        private async void OnSaveProfileClicked(object? sender, RoutedEventArgs e)
        {
            CaptureCurrentDirectories();
            await _ProfileStore.UpsertAsync(_Profile, CancellationToken.None).ConfigureAwait(true);
        }

        private async void OnAddTabClicked(object? sender, RoutedEventArgs e)
        {
            TerminalTabEditorWindow editor = new TerminalTabEditorWindow(null, _ShellCatalog.GetSupportedShells(), _ColorSchemes);
            TerminalTabProfile? tab = await editor.ShowDialog<TerminalTabProfile?>(this).ConfigureAwait(true);
            if (tab == null) return;
            AddTerminalTab(tab, false);
        }

        private async void OnEditTabClicked(object? sender, RoutedEventArgs e)
        {
            TerminalSession? session = GetSelectedSession();
            if (session == null) return;

            await EditSessionTabAsync(session).ConfigureAwait(true);
        }

        private async Task EditSessionTabAsync(TerminalSession session)
        {
            Int32 index = _Sessions.IndexOf(session);
            if (index < 0 || index >= _Sessions.Count) return;

            TerminalTabEditorWindow editor = new TerminalTabEditorWindow(session.TabProfile, _ShellCatalog.GetSupportedShells(), _ColorSchemes);
            TerminalTabProfile? updated = await editor.ShowDialog<TerminalTabProfile?>(this).ConfigureAwait(true);
            if (updated == null) return;

            Int32 profileIndex = _Profile.Tabs.IndexOf(session.TabProfile);
            if (profileIndex >= 0)
            {
                _Profile.Tabs[profileIndex] = updated;
            }

            session.TabProfile = updated;
            ApplyTerminalAppearance(session.Terminal, _Profile, updated, updated.ColorSchemeOverride ?? _Profile.GlobalColorScheme);
            ReplaceTabHeader(session);
        }

        private void CaptureCurrentDirectories()
        {
            foreach (TerminalSession session in _Sessions)
            {
                if (!String.IsNullOrWhiteSpace(session.Terminal.CurrentDirectory))
                {
                    session.TabProfile.StartingDirectory = session.Terminal.CurrentDirectory;
                }
            }
        }

        private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_IsClosingWorkspace) return;

            if (!_HasConfirmedWorkspaceClose)
            {
                e.Cancel = true;
                if (_IsCloseConfirmationOpen) return;

                _IsCloseConfirmationOpen = true;
                WorkspaceCloseConfirmationWindow confirmation = new WorkspaceCloseConfirmationWindow(_Profile.Name, _Sessions.Count);
                bool confirmed = await confirmation.ShowDialog<bool>(this).ConfigureAwait(true);
                _IsCloseConfirmationOpen = false;
                if (!confirmed) return;

                _HasConfirmedWorkspaceClose = true;
                Close();
                return;
            }

            _IsClosingWorkspace = true;
            foreach (TerminalSession session in _Sessions.ToList())
            {
                session.IsClosingByTermrig = true;
                UnwireTerminalSessionEvents(session);
                QueueTerminalKill(session);
            }

            _Sessions.Clear();
        }

        private void OnTerminalProcessExited(object? sender, ProcessExitedEventArgs e)
        {
            if (!(sender is TerminalControl terminal)) return;

            TerminalSession? session = _Sessions.FirstOrDefault(item => item.Terminal == terminal);
            if (session == null) return;
            if (session.IsClosingByTermrig) return;
            if (e.ExitCode == 0) return;

            string details =
                "Terminal process exited unexpectedly." + Environment.NewLine +
                "Profile: " + _Profile.Name + Environment.NewLine +
                "Tab: " + session.TabProfile.Name + Environment.NewLine +
                "Shell: " + session.TabProfile.Shell + Environment.NewLine +
                "Exit code: " + e.ExitCode + Environment.NewLine +
                "Directory: " + session.TabProfile.StartingDirectory + Environment.NewLine +
                "Startup script: " + session.TabProfile.StartupScript;
            WriteTerminalCrashLog(session.TabProfile, "Terminal process exited with non-zero exit code.", details);
        }

        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.W) return;
            if (e.KeyModifiers != KeyModifiers.Control) return;

            e.Handled = true;
            CloseSelectedTab();
        }

        private void OnWindowTextInput(object? sender, TextInputEventArgs e)
        {
            if (!_SuppressNextShortcutTextInput) return;

            _SuppressNextShortcutTextInput = false;
            if (e.Text != "+" && e.Text != "=" && e.Text != "-") return;
            e.Handled = true;
        }

        private async void OnWindowPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            TerminalSession? session = GetSelectedSession();
            if (session == null) return;

            if (TryHandleTerminalControlShortcut(session, e))
            {
                return;
            }

            if (e.Key == Key.V && (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control)
            {
                e.Handled = true;
                await PasteIntoTerminalAsync(session.Terminal).ConfigureAwait(true);
                return;
            }

            bool hasSelection = session.Terminal.HasSelection;
            if (!hasSelection) return;

            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                e.Handled = true;
                return;
            }

            if (e.Key != Key.C) return;
            if ((e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.Control) return;

            string selectedText = session.Terminal.GetSelectedText();
            if (String.IsNullOrEmpty(selectedText)) return;

            e.Handled = true;
            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel?.Clipboard != null)
            {
                DataTransfer data = new DataTransfer();
                data.Add(DataTransferItem.CreateText(selectedText));
                await topLevel.Clipboard.SetDataAsync(data).ConfigureAwait(true);
            }

            session.Terminal.ClearSelection();
        }

        private bool TryHandleTerminalControlShortcut(TerminalSession session, KeyEventArgs e)
        {
            if ((e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.Control) return false;
            if ((e.KeyModifiers & KeyModifiers.Alt) == KeyModifiers.Alt) return false;

            if (IsIncreaseFontKey(e))
            {
                e.Handled = true;
                _SuppressNextShortcutTextInput = true;
                AdjustTerminalFontSize(session, TerminalFontZoomStep);
                return true;
            }

            if (IsDecreaseFontKey(e))
            {
                e.Handled = true;
                _SuppressNextShortcutTextInput = true;
                AdjustTerminalFontSize(session, -TerminalFontZoomStep);
                return true;
            }

            if (IsResetFontKey(e))
            {
                e.Handled = true;
                _SuppressNextShortcutTextInput = true;
                SetTerminalFontSize(session, session.RuntimeDefaultFontSize);
                return true;
            }

            return false;
        }

        private static bool IsIncreaseFontKey(KeyEventArgs e)
        {
            return
                e.Key == Key.OemPlus ||
                e.Key == Key.Add ||
                e.PhysicalKey == PhysicalKey.Equal ||
                e.PhysicalKey == PhysicalKey.NumPadAdd ||
                e.KeySymbol == "+" ||
                e.KeySymbol == "=";
        }

        private static bool IsDecreaseFontKey(KeyEventArgs e)
        {
            return
                e.Key == Key.OemMinus ||
                e.Key == Key.Subtract ||
                e.PhysicalKey == PhysicalKey.Minus ||
                e.PhysicalKey == PhysicalKey.NumPadSubtract ||
                e.KeySymbol == "-";
        }

        private static bool IsResetFontKey(KeyEventArgs e)
        {
            return
                e.Key == Key.D0 ||
                e.Key == Key.NumPad0 ||
                e.PhysicalKey == PhysicalKey.Digit0 ||
                e.PhysicalKey == PhysicalKey.NumPad0;
        }

        private void AdjustTerminalFontSize(TerminalSession session, double delta)
        {
            SetTerminalFontSize(session, session.Terminal.FontSize + delta);
        }

        private void SetTerminalFontSize(TerminalSession session, double fontSize)
        {
            double clamped = Math.Clamp(fontSize, MinimumTerminalFontSize, MaximumTerminalFontSize);
            session.Terminal.FontSize = clamped;
            UpdateProfileFontSize(session, clamped);
            session.Terminal.ClearVisibleLineCaches();
            session.Terminal.RequestRenderInvalidate();
        }

        private void UpdateProfileFontSize(TerminalSession session, double fontSize)
        {
            session.TabProfile.FontSize = fontSize;

            if (session.IsProfileMember)
            {
                _ = _ProfileStore.UpsertAsync(_Profile, CancellationToken.None);
            }
        }

        private Control BuildTabHeader(TerminalSession session)
        {
            Border container = new Border
            {
                BorderBrush = Brush.Parse("#35424F"),
                BorderThickness = new Avalonia.Thickness(1, 1, 1, 0),
                CornerRadius = new Avalonia.CornerRadius(3, 3, 0, 0),
                Padding = new Avalonia.Thickness(5, 1),
                Background = Brush.Parse("#121820"),
                MinHeight = 22
            };
            container.Tag = session;
            container.PointerPressed += OnTabHeaderPointerPressed;
            container.DoubleTapped += OnTabHeaderDoubleTapped;

            StackPanel panel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4
            };

            TextBlock attentionIndicator = new TextBlock
            {
                Text = "!",
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = Brush.Parse("#F85149"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Width = 8,
                IsVisible = session.HasAttention,
                Tag = AttentionIndicatorTag
            };
            ToolTip.SetTip(attentionIndicator, "Terminal bell");
            panel.Children.Add(attentionIndicator);

            TextBlock title = new TextBlock
            {
                Text = session.TabProfile.Name,
                FontSize = 10,
                Foreground = Brush.Parse("#E6EDF3"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Tag = session
            };
            panel.Children.Add(title);

            if (!session.IsProfileMember)
            {
                Button profileButton = new Button
                {
                    Content = "*",
                    Classes = { "icon" },
                    Width = 20,
                    Height = 20,
                    FontSize = 9,
                    Tag = session
                };
                ToolTip.SetTip(profileButton, "Profile actions");
                profileButton.Click += OnUnsavedTabMenuClicked;
                panel.Children.Add(profileButton);
            }

            Button closeButton = new Button
            {
                Content = "X",
                Classes = { "icon" },
                Width = 20,
                Height = 20,
                FontSize = 9,
                Tag = session
            };
            ToolTip.SetTip(closeButton, "Close tab");
            closeButton.Click += OnCloseTabClicked;
            panel.Children.Add(closeButton);

            container.Child = panel;
            return container;
        }

        private async void OnTabHeaderDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (e.Source is Control source && (source is Button || source.FindAncestorOfType<Button>() != null)) return;
            if (!(sender is Control control)) return;
            if (!(control.Tag is TerminalSession session)) return;

            SelectSession(session);
            await EditSessionTabAsync(session).ConfigureAwait(true);
        }

        private async void OnTabHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.Source is Control source && (source is Button || source.FindAncestorOfType<Button>() != null)) return;
            if (!(sender is Control control)) return;
            if (!(control.Tag is TerminalSession session)) return;

            SelectSession(session);
            _DraggedSession = session;
            MarkDraggedHeader(session);
            try
            {
                DataTransfer data = new DataTransfer();
                data.Add(DataTransferItem.CreateText("termrig-workspace-tab:" + _Sessions.IndexOf(session)));
                await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move).ConfigureAwait(true);
            }
            finally
            {
                ClearHeaderDragVisuals();
                _DraggedSession = null;
            }
        }

        private void OnTabHeadersDragOver(object? sender, DragEventArgs e)
        {
            e.DragEffects = _DraggedSession != null ? DragDropEffects.Move : DragDropEffects.None;
            MarkDropTargetHeader(GetTabHeaderDropTarget(e), e.DragEffects != DragDropEffects.None);
            e.Handled = true;
        }

        private async void OnTabHeadersDrop(object? sender, DragEventArgs e)
        {
            if (_DraggedSession == null) return;

            Int32 sourceIndex = _Sessions.IndexOf(_DraggedSession);
            if (sourceIndex < 0) return;

            Int32 targetIndex = GetTabHeaderDropIndex(e);
            _Sessions.RemoveAt(sourceIndex);
            TerminalTabHeaders.Children.Remove(_DraggedSession.Header);
            if (sourceIndex < targetIndex) targetIndex--;
            targetIndex = Math.Clamp(targetIndex, 0, _Sessions.Count);
            _Sessions.Insert(targetIndex, _DraggedSession);
            TerminalTabHeaders.Children.Insert(targetIndex, _DraggedSession.Header);
            SelectSession(_DraggedSession);
            await PersistProfileTabOrderAsync().ConfigureAwait(true);
            ClearDropTargetHeader();
            e.Handled = true;
        }

        private void OnTabHeadersDragLeave(object? sender, DragEventArgs e)
        {
            ClearDropTargetHeader();
        }

        private Int32 GetTabHeaderDropIndex(DragEventArgs e)
        {
            if (e.Source is Control source)
            {
                Control? current = source;
                while (current != null)
                {
                    if (current is Border border && border.Tag is TerminalSession session)
                    {
                        Int32 index = _Sessions.IndexOf(session);
                        if (index < 0) return _Sessions.Count;
                        return e.GetPosition(border).X > border.Bounds.Width / 2 ? index + 1 : index;
                    }

                    current = current.GetVisualParent() as Control;
                }
            }

            return _Sessions.Count;
        }

        private Control? GetTabHeaderDropTarget(DragEventArgs e)
        {
            if (e.Source is Control source)
            {
                Control? current = source;
                while (current != null)
                {
                    if (current is Border border && border.Tag is TerminalSession)
                    {
                        return border;
                    }

                    current = current.GetVisualParent() as Control;
                }
            }

            return null;
        }

        private void MarkDraggedHeader(TerminalSession session)
        {
            if (session.Header is Control header)
            {
                header.Opacity = 0.55;
                header.Cursor = new Cursor(StandardCursorType.SizeAll);
            }
        }

        private void MarkDropTargetHeader(Control? header, bool isValidTarget)
        {
            if (!isValidTarget || header == null || (_DraggedSession != null && header == _DraggedSession.Header))
            {
                ClearDropTargetHeader();
                return;
            }

            if (_DropTargetHeader == header) return;

            ClearDropTargetHeader();
            _DropTargetHeader = header;
            header.Opacity = 0.78;
            header.Cursor = new Cursor(StandardCursorType.SizeAll);
        }

        private void ClearHeaderDragVisuals()
        {
            if (_DraggedSession?.Header is Control header)
            {
                header.Opacity = 1;
                header.Cursor = null;
            }

            ClearDropTargetHeader();
        }

        private void ClearDropTargetHeader()
        {
            if (_DropTargetHeader == null) return;
            if (_DropTargetHeader != _DraggedSession?.Header) _DropTargetHeader.Opacity = 1;
            _DropTargetHeader.Cursor = null;
            _DropTargetHeader = null;
        }

        private async Task PersistProfileTabOrderAsync()
        {
            List<TerminalTabProfile> reorderedTabs = _Sessions
                .Where(item => item.IsProfileMember)
                .Select(item => item.TabProfile)
                .ToList();
            if (reorderedTabs.Count < 1) return;

            List<TerminalTabProfile> existingTabs = _Profile.Tabs.ToList();
            Int32 insertIndex = existingTabs
                .Select((item, index) => new { item, index })
                .Where(item => reorderedTabs.Contains(item.item))
                .Select(item => item.index)
                .DefaultIfEmpty(0)
                .Min();

            _Profile.Tabs.RemoveAll(item => reorderedTabs.Contains(item));
            insertIndex = Math.Clamp(insertIndex, 0, _Profile.Tabs.Count);
            _Profile.Tabs.InsertRange(insertIndex, reorderedTabs);
            CaptureCurrentDirectories();
            await _ProfileStore.UpsertAsync(_Profile, CancellationToken.None).ConfigureAwait(true);
        }

        private void OnUnsavedTabMenuClicked(object? sender, RoutedEventArgs e)
        {
            if (!(sender is Control control)) return;
            if (!(control.Tag is TerminalSession session)) return;

            ContextMenu menu = new ContextMenu();
            MenuItem addItem = new MenuItem
            {
                Header = "Add to profile",
                Tag = session
            };
            addItem.Click += OnAddTransientTabToProfileClicked;
            menu.Items.Add(addItem);
            control.ContextMenu = menu;
            menu.Open(control);
        }

        private async void OnAddTransientTabToProfileClicked(object? sender, RoutedEventArgs e)
        {
            if (!(sender is Control control)) return;
            if (!(control.Tag is TerminalSession session)) return;
            if (session.IsProfileMember) return;

            CaptureSessionDirectory(session);
            _Profile.Tabs.Add(session.TabProfile);
            session.IsProfileMember = true;
            ReplaceTabHeader(session);
            await _ProfileStore.UpsertAsync(_Profile, CancellationToken.None).ConfigureAwait(true);
        }

        private void OnCloseTabClicked(object? sender, RoutedEventArgs e)
        {
            if (!(sender is Control control)) return;
            if (!(control.Tag is TerminalSession session)) return;

            CloseSession(session);
        }

        private void CloseSelectedTab()
        {
            TerminalSession? session = GetSelectedSession();
            if (session == null) return;
            CloseSession(session);
        }

        private void CloseSession(TerminalSession session)
        {
            if (_IsClosingWorkspace) return;
            int removedIndex = _Sessions.IndexOf(session);
            session.IsClosingByTermrig = true;
            UnwireTerminalSessionEvents(session);

            _Sessions.Remove(session);
            TerminalTabHeaders.Children.Remove(session.Header);
            TerminalSurface.Children.Remove(session.Terminal);
            QueueTerminalKill(session);

            if (_Sessions.Count < 1)
            {
                Close();
                return;
            }

            if (_SelectedSession == session)
            {
                int nextIndex = Math.Clamp(removedIndex, 0, _Sessions.Count - 1);
                SelectSession(_Sessions[nextIndex]);
            }
            else
            {
                UpdateTabHeaderStates();
            }
        }

        private void SelectSession(TerminalSession session)
        {
            if (!_Sessions.Contains(session)) return;
            _SelectedSession = session;
            session.HasAttention = false;

            foreach (TerminalSession item in _Sessions)
            {
                bool selected = item == session;
                item.Terminal.Opacity = selected ? 1 : 0;
                item.Terminal.IsHitTestVisible = selected;
                item.Terminal.SetValue(Panel.ZIndexProperty, selected ? 1 : 0);
            }

            UpdateTabHeaderStates();
            FocusTerminal(session);
        }

        private void FocusSelectedTerminal()
        {
            TerminalSession? session = GetSelectedSession();
            if (session == null) return;
            FocusTerminal(session);
        }

        private TerminalSession? GetSelectedSession()
        {
            return _SelectedSession;
        }

        private void ReplaceTabHeader(TerminalSession session)
        {
            int index = _Sessions.IndexOf(session);
            if (index < 0) return;

            TerminalTabHeaders.Children.Remove(session.Header);
            session.Header = BuildTabHeader(session);
            TerminalTabHeaders.Children.Insert(index, session.Header);
            UpdateTabHeaderStates();
        }

        private void UpdateTabHeaderStates()
        {
            foreach (TerminalSession session in _Sessions)
            {
                if (session.Header is Border border)
                {
                    bool selected = session == _SelectedSession;
                    border.Background = Brush.Parse(selected ? "#1F6FEB" : session.HasAttention ? "#27161A" : "#121820");
                    border.BorderBrush = Brush.Parse(selected ? "#58A6FF" : session.HasAttention ? "#F85149" : "#35424F");
                    SetAttentionIndicatorVisibility(border, session.HasAttention);
                }
            }
        }

        private static void SetAttentionIndicatorVisibility(Border header, bool isVisible)
        {
            if (!(header.Child is StackPanel panel)) return;

            foreach (Control child in panel.Children)
            {
                if (child.Tag as string == AttentionIndicatorTag)
                {
                    child.IsVisible = isVisible;
                    return;
                }
            }
        }

        private void OnTerminalBellRang(object? sender, RoutedEventArgs e)
        {
            TerminalSession? session = FindSessionForTerminalEvent(sender, e);
            if (session == null) return;

            if (session == _SelectedSession && IsActive)
            {
                ClearAttention(session);
            }
            else
            {
                MarkAttention(session);
            }

            e.Handled = true;
        }

        private TerminalSession? FindSessionForTerminalEvent(object? sender, RoutedEventArgs e)
        {
            if (sender is TerminalControl terminal)
            {
                return _Sessions.FirstOrDefault(item => item.Terminal == terminal);
            }

            if (e.Source is TerminalControl sourceTerminal)
            {
                return _Sessions.FirstOrDefault(item => item.Terminal == sourceTerminal);
            }

            if (e.Source is Control sourceControl)
            {
                TerminalControl? parentTerminal = sourceControl.FindAncestorOfType<TerminalControl>();
                if (parentTerminal != null)
                {
                    return _Sessions.FirstOrDefault(item => item.Terminal == parentTerminal);
                }
            }

            return null;
        }

        private void MarkAttention(TerminalSession session)
        {
            if (session.HasAttention) return;

            session.HasAttention = true;
            UpdateTabHeaderStates();
        }

        private void ClearAttention(TerminalSession? session)
        {
            if (session == null) return;
            if (!session.HasAttention) return;

            session.HasAttention = false;
            UpdateTabHeaderStates();
        }

        private void WireTerminalSessionEvents(TerminalSession session)
        {
            session.Terminal.ProcessExited += OnTerminalProcessExited;
            TerminalView.AddBellRangHandler(session.Terminal, OnTerminalBellRang);
        }

        private void UnwireTerminalSessionEvents(TerminalSession session)
        {
            session.Terminal.ProcessExited -= OnTerminalProcessExited;
            TerminalView.RemoveBellRangHandler(session.Terminal, OnTerminalBellRang);
        }

        private static void FocusTerminal(TerminalSession session)
        {
            Dispatcher.UIThread.Post(delegate
            {
                session.Terminal.Focus();
            }, DispatcherPriority.Input);
        }

        private static async Task PasteIntoTerminalAsync(TerminalControl terminal)
        {
            await terminal.PasteAsync().ConfigureAwait(true);
        }

        private async Task RunStartupCommandsAsync(TerminalSession session, List<string> startupCommands)
        {
            if (startupCommands.Count < 1) return;

            try
            {
                await Task.Delay(250).ConfigureAwait(true);
                foreach (string command in startupCommands)
                {
                    if (session.IsClosingByTermrig) return;
                    await SendToTerminalAsync(session.Terminal, command + "\r").ConfigureAwait(true);
                    await Task.Delay(50).ConfigureAwait(true);
                }
            }
            catch (Exception exception)
            {
                WriteTerminalCrashLog(session.TabProfile, "Terminal startup script failed.", exception.ToString());
            }
        }

        private static async Task SendToTerminalAsync(TerminalControl terminal, string text)
        {
            await terminal.SendTextAsync(text, CancellationToken.None).ConfigureAwait(true);
        }

        private void QueueTerminalKill(TerminalSession session)
        {
            _ = Task.Run(delegate
            {
                try
                {
                    KillTerminalProcess(session.Terminal);
                }
                catch (Exception exception)
                {
                    WriteTerminalCrashLog(session.TabProfile, "Terminal close failed.", exception.ToString());
                }
            });
        }

        private static void KillTerminalProcess(TerminalControl terminal)
        {
            terminal.TerminateSession();
        }

        private void CaptureSessionDirectory(TerminalSession session)
        {
            if (!String.IsNullOrWhiteSpace(session.Terminal.CurrentDirectory))
            {
                session.TabProfile.StartingDirectory = session.Terminal.CurrentDirectory;
            }
        }

        private static void ApplyTerminalAppearance(TerminalControl terminal, TerminalProfile profile, TerminalTabProfile tab, ColorScheme scheme)
        {
            string fontFamily = tab.FontFamily ?? profile.FontFamily ?? GetDefaultTerminalFontFamily();
            double fontSize = tab.FontSize ?? profile.FontSize ?? Constants.DefaultTerminalFontSize;
            ApplyFontFamily(terminal, fontFamily);
            terminal.FontSize = fontSize;
            terminal.BufferSize = ResolveTerminalBufferSize(tab);
            terminal.Options = BuildTerminalOptions(tab);
            terminal.Background = Brush.Parse(scheme.Background);
            terminal.Foreground = Brush.Parse(scheme.Foreground);
            terminal.RecordPtyOutput = tab.RecordPtyOutput;
            terminal.PtyRecordingDirectory = tab.PtyRecordingDirectory;
        }

        private static int ResolveTerminalBufferSize(TerminalTabProfile tab)
        {
            return tab.ScrollbackBufferSize ?? Constants.DefaultTerminalBufferSize;
        }

        private static XTerm.Options.TerminalOptions BuildTerminalOptions(TerminalTabProfile tab)
        {
            return new XTerm.Options.TerminalOptions
            {
                ConvertEol = !OperatingSystem.IsWindows(),
                Scrollback = ResolveTerminalBufferSize(tab),
                TermName = "xterm-256color"
            };
        }

        private static void ApplyFontFamily(TerminalControl terminal, string fontFamily)
        {
            string normalized = fontFamily;
            if (fontFamily.Contains(",", StringComparison.Ordinal))
            {
                string[] parts = fontFamily.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length > 0) normalized = parts[0];
            }

            try
            {
                terminal.FontFamily = new FontFamily(normalized);
            }
            catch (FormatException)
            {
            }
        }

        private static string GetDefaultTerminalFontFamily()
        {
            if (OperatingSystem.IsWindows()) return "Consolas";
            if (OperatingSystem.IsMacOS()) return "Menlo";
            return "DejaVu Sans Mono";
        }

        private async void ShowTerminalLaunchError(TerminalTabProfile tab, ShellLaunchPlan plan, Exception exception)
        {
            string details = BuildLaunchFailureDetails(tab, plan, exception);
            TextPromptWindow prompt = new TextPromptWindow(
                "Terminal launch failed",
                "Could not open tab \"" + tab.Name + "\"",
                details);
            await prompt.ShowDialog<string?>(this).ConfigureAwait(true);
        }

        private void WriteTerminalCrashLog(TerminalTabProfile tab, string summary, string details)
        {
            _CrashLogStore.Write(_Profile.Name, tab.Name, summary, details);
        }

        private static string BuildLaunchFailureDetails(TerminalTabProfile tab, ShellLaunchPlan plan, Exception exception)
        {
            return
                "Tab: " + tab.Name + Environment.NewLine +
                "Executable: " + plan.Executable + Environment.NewLine +
                "Directory: " + plan.StartingDirectory + Environment.NewLine +
                "Args: " + String.Join(" ", plan.Arguments) + Environment.NewLine +
                "Error: " + exception;
        }

        #endregion
    }
}
