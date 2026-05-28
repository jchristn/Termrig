namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Input;
    using Avalonia.Media;
    using Avalonia.Threading;
    using Iciclecreek.Terminal;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
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
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(profileStore);
            ArgumentNullException.ThrowIfNull(shellCatalog);
            ArgumentNullException.ThrowIfNull(colorSchemes);

            _Profile = profile;
            _ProfileStore = profileStore;
            _ShellCatalog = shellCatalog;
            _ColorSchemes = colorSchemes;

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
            TerminalTabs.SelectionChanged += OnTerminalTabSelectionChanged;
            AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
            Closing += OnWindowClosing;
        }

        private void LaunchProfile()
        {
            foreach (TerminalTabProfile tab in _Profile.Tabs)
            {
                AddTerminalTab(tab, true);
            }
        }

        private void AddTerminalTab(TerminalTabProfile tab, bool isProfileMember)
        {
            ShellLaunchPlan plan = _ShellCatalog.BuildLaunchPlan(tab);
            ColorScheme scheme = tab.ColorSchemeOverride ?? _Profile.GlobalColorScheme;

            TerminalControl terminal = new TerminalControl
            {
                BufferSize = 2000,
                Background = Brush.Parse(scheme.Background),
                Foreground = Brush.Parse(scheme.Foreground),
                Focusable = true
            };
            terminal.PointerPressed += OnTerminalPointerPressed;
            ApplyTerminalAppearance(terminal, _Profile, tab, scheme);

            TerminalSession session = new TerminalSession
            {
                TabProfile = tab,
                Terminal = terminal,
                IsProfileMember = isProfileMember
            };

            TabItem item = new TabItem
            {
                Header = BuildTabHeader(session),
                Content = terminal
            };

            session.TabItem = item;
            terminal.ProcessExited += OnTerminalProcessExited;
            _Sessions.Add(session);
            TerminalTabs.Items.Add(item);
            TerminalTabs.SelectedItem = item;
            try
            {
                terminal.LaunchProcess(plan.StartingDirectory, plan.Executable, plan.Arguments.ToArray());
                FocusTerminal(session);
            }
            catch (Exception exception)
            {
                TerminalTabs.Items.Remove(item);
                _Sessions.Remove(session);
                terminal.ProcessExited -= OnTerminalProcessExited;
                terminal.PointerPressed -= OnTerminalPointerPressed;
                WriteTerminalCrashLog(tab, "Terminal launch failed.", BuildLaunchFailureDetails(tab, plan, exception));
                ShowTerminalLaunchError(tab, plan, exception);
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
            Int32 index = TerminalTabs.SelectedIndex;
            if (index < 0 || index >= _Sessions.Count) return;

            TerminalSession session = _Sessions[index];
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
            session.TabItem.Header = BuildTabHeader(session);
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

        private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            foreach (TerminalSession session in _Sessions)
            {
                session.IsClosingByTermrig = true;
                session.Terminal.Kill();
            }
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

        private void OnTerminalTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            FocusSelectedTerminal();
        }

        private void OnTerminalPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!(sender is TerminalControl terminal)) return;
            terminal.Focus();
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

            StackPanel panel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4
            };

            TextBlock title = new TextBlock
            {
                Text = session.TabProfile.Name,
                FontSize = 10,
                Foreground = Brush.Parse("#E6EDF3"),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Tag = session
            };
            title.DoubleTapped += OnTabTitleDoubleTapped;
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

        private async void OnTabTitleDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (!(sender is Control control)) return;
            if (!(control.Tag is TerminalSession session)) return;

            TextPromptWindow prompt = new TextPromptWindow("Rename tab", "Tab name", session.TabProfile.Name);
            string? name = await prompt.ShowDialog<string?>(this).ConfigureAwait(true);
            if (String.IsNullOrWhiteSpace(name)) return;

            session.TabProfile.Name = name;
            session.TabItem.Header = BuildTabHeader(session);
            if (session.IsProfileMember)
            {
                await _ProfileStore.UpsertAsync(_Profile, CancellationToken.None).ConfigureAwait(true);
            }
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
            session.TabItem.Header = BuildTabHeader(session);
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
            Int32 index = TerminalTabs.SelectedIndex;
            if (index < 0 || index >= _Sessions.Count) return;
            CloseSession(_Sessions[index]);
        }

        private void CloseSession(TerminalSession session)
        {
            session.IsClosingByTermrig = true;
            session.Terminal.ProcessExited -= OnTerminalProcessExited;
            session.Terminal.PointerPressed -= OnTerminalPointerPressed;
            session.Terminal.Kill();
            _Sessions.Remove(session);
            TerminalTabs.Items.Remove(session.TabItem);

            if (_Sessions.Count < 1)
            {
                Close();
            }
        }

        private void FocusSelectedTerminal()
        {
            Int32 index = TerminalTabs.SelectedIndex;
            if (index < 0 || index >= _Sessions.Count) return;
            FocusTerminal(_Sessions[index]);
        }

        private static void FocusTerminal(TerminalSession session)
        {
            Dispatcher.UIThread.Post(delegate
            {
                session.Terminal.Focus();
            }, DispatcherPriority.Input);
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
            terminal.Background = Brush.Parse(scheme.Background);
            terminal.Foreground = Brush.Parse(scheme.Foreground);
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
