namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Interactivity;
    using Avalonia.Input;
    using Avalonia.Media;
    using Iciclecreek.Terminal;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Termrig.App.Models;
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
        private readonly List<ColorScheme> _ColorSchemes = ColorSchemeCatalog.GetSchemes();
        private readonly List<TerminalSession> _Sessions = new List<TerminalSession>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the terminal workspace window for the XAML loader.
        /// </summary>
        public TerminalWorkspaceWindow()
            : this(new TerminalProfile { Name = "Default" }, new ProfileStore(), new ShellCatalog())
        {
        }

        /// <summary>
        /// Instantiate the terminal workspace window.
        /// </summary>
        /// <param name="profile">Profile to open.</param>
        /// <param name="profileStore">Profile store used for save operations.</param>
        /// <param name="shellCatalog">Shell catalog.</param>
        /// <exception cref="ArgumentNullException">Thrown when required inputs are null.</exception>
        public TerminalWorkspaceWindow(TerminalProfile profile, ProfileStore profileStore, ShellCatalog shellCatalog)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(profileStore);
            ArgumentNullException.ThrowIfNull(shellCatalog);

            _Profile = profile;
            _ProfileStore = profileStore;
            _ShellCatalog = shellCatalog;

            InitializeComponent();
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
                Foreground = Brush.Parse(scheme.Foreground)
            };
            ApplyTerminalAppearance(terminal, tab, scheme);

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
            _Sessions.Add(session);
            TerminalTabs.Items.Add(item);
            TerminalTabs.SelectedItem = item;
            terminal.LaunchProcess(plan.StartingDirectory, plan.Executable, plan.Arguments.ToArray());
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
            ApplyTerminalAppearance(session.Terminal, updated, updated.ColorSchemeOverride ?? _Profile.GlobalColorScheme);
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
                session.Terminal.Kill();
            }
        }

        private Control BuildTabHeader(TerminalSession session)
        {
            Border container = new Border
            {
                BorderBrush = Brush.Parse("#AEB8C4"),
                BorderThickness = new Avalonia.Thickness(1, 1, 1, 0),
                CornerRadius = new Avalonia.CornerRadius(4, 4, 0, 0),
                Padding = new Avalonia.Thickness(8, 4),
                Background = Brush.Parse("#F4F7FA")
            };

            StackPanel panel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 6
            };

            TextBlock title = new TextBlock
            {
                Text = session.TabProfile.Name,
                FontSize = 12,
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
                    Padding = new Avalonia.Thickness(5, 0),
                    FontSize = 11,
                    Tag = session
                };
                ToolTip.SetTip(profileButton, "Profile actions");
                profileButton.Click += OnUnsavedTabMenuClicked;
                panel.Children.Add(profileButton);
            }

            Button closeButton = new Button
            {
                Content = "X",
                Padding = new Avalonia.Thickness(5, 0),
                FontSize = 11,
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

            session.Terminal.Kill();
            _Sessions.Remove(session);
            TerminalTabs.Items.Remove(session.TabItem);
            if (session.IsProfileMember)
            {
                _Profile.Tabs.Remove(session.TabProfile);
            }
        }

        private void CaptureSessionDirectory(TerminalSession session)
        {
            if (!String.IsNullOrWhiteSpace(session.Terminal.CurrentDirectory))
            {
                session.TabProfile.StartingDirectory = session.Terminal.CurrentDirectory;
            }
        }

        private static void ApplyTerminalAppearance(TerminalControl terminal, TerminalTabProfile tab, ColorScheme scheme)
        {
            terminal.FontFamily = new FontFamily(tab.FontFamily);
            terminal.FontSize = tab.FontSize;
            terminal.Background = Brush.Parse(scheme.Background);
            terminal.Foreground = Brush.Parse(scheme.Foreground);
        }

        #endregion
    }
}
