namespace Termrig.App.Services
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Input;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Termrig.App.Views;

    /// <summary>
    /// Builds the macOS menu bar (application, File, Edit, View, Window and Help menus).
    /// </summary>
    internal static class MacOSMenu
    {
        #region Internal-Members

        internal const string RepositoryUrl = "https://github.com/jchristn/Termrig";
        internal const string IssuesUrl = "https://github.com/jchristn/Termrig/issues";
        internal const string DiscordUrl = "https://discord.gg/tRAN8HgvK5";

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Install the application menu. Avalonia appends the standard Services, Hide, Hide Others,
        /// Show All and Quit items after these.
        /// </summary>
        /// <param name="application">Application.</param>
        internal static void ApplyApplicationMenu(Application application)
        {
            if (!OperatingSystem.IsMacOS()) return;

            NativeMenu menu = new NativeMenu();
            menu.Items.Add(CreateAsyncItem("About Termrig", ShowAboutAsync));
            NativeMenu.SetMenu(application, menu);
        }

        /// <summary>
        /// Install the menu bar for a window. File and View menus are window specific; Edit is
        /// supplied by the window so it can route commands to its focused control.
        /// </summary>
        /// <param name="window">Window that owns the menu bar.</param>
        /// <param name="fileMenu">File menu.</param>
        /// <param name="editMenu">Edit menu.</param>
        /// <param name="viewMenu">Optional View menu.</param>
        internal static void ApplyWindowMenu(Window window, NativeMenuItem fileMenu, NativeMenuItem editMenu, NativeMenuItem? viewMenu)
        {
            if (!OperatingSystem.IsMacOS()) return;

            NativeMenu menu = new NativeMenu();
            menu.Items.Add(fileMenu);
            menu.Items.Add(editMenu);
            if (viewMenu != null) menu.Items.Add(viewMenu);
            menu.Items.Add(CreateWindowMenu(window));
            menu.Items.Add(CreateHelpMenu());
            NativeMenu.SetMenu(window, menu);
        }

        /// <summary>
        /// Create an Edit menu that applies Undo/Redo/Cut/Copy/Paste/Select All to the focused text box.
        /// </summary>
        /// <param name="window">Window whose focused control receives the commands.</param>
        /// <returns>Edit menu.</returns>
        internal static NativeMenuItem CreateTextEditMenu(Window window)
        {
            return CreateSubmenu("Edit",
                CreateItem("Undo", delegate { GetFocusedTextBox(window)?.Undo(); }, Cmd(Key.Z)),
                CreateItem("Redo", delegate { GetFocusedTextBox(window)?.Redo(); }, Cmd(Key.Z, KeyModifiers.Shift)),
                new NativeMenuItemSeparator(),
                CreateItem("Cut", delegate { GetFocusedTextBox(window)?.Cut(); }, Cmd(Key.X)),
                CreateItem("Copy", delegate { GetFocusedTextBox(window)?.Copy(); }, Cmd(Key.C)),
                CreateItem("Paste", delegate { GetFocusedTextBox(window)?.Paste(); }, Cmd(Key.V)),
                CreateItem("Select All", delegate { GetFocusedTextBox(window)?.SelectAll(); }, Cmd(Key.A)));
        }

        /// <summary>
        /// Create a submenu.
        /// </summary>
        /// <param name="header">Menu title.</param>
        /// <param name="items">Menu items.</param>
        /// <returns>Submenu item.</returns>
        internal static NativeMenuItem CreateSubmenu(string header, params NativeMenuItemBase[] items)
        {
            NativeMenu menu = new NativeMenu();
            foreach (NativeMenuItemBase item in items)
            {
                menu.Items.Add(item);
            }

            return new NativeMenuItem(header) { Menu = menu };
        }

        /// <summary>
        /// Create a menu item.
        /// </summary>
        /// <param name="header">Item text.</param>
        /// <param name="onClick">Click action.</param>
        /// <param name="gesture">Optional keyboard shortcut.</param>
        /// <returns>Menu item.</returns>
        internal static NativeMenuItem CreateItem(string header, Action onClick, KeyGesture? gesture = null)
        {
            NativeMenuItem item = new NativeMenuItem(header) { Gesture = gesture };
            item.Click += delegate { onClick(); };
            return item;
        }

        /// <summary>
        /// Create a menu item that runs an asynchronous action.
        /// </summary>
        /// <param name="header">Item text.</param>
        /// <param name="onClick">Click action.</param>
        /// <param name="gesture">Optional keyboard shortcut.</param>
        /// <returns>Menu item.</returns>
        internal static NativeMenuItem CreateAsyncItem(string header, Func<Task> onClick, KeyGesture? gesture = null)
        {
            NativeMenuItem item = new NativeMenuItem(header) { Gesture = gesture };
            item.Click += async delegate { await onClick().ConfigureAwait(true); };
            return item;
        }

        /// <summary>
        /// Create a Command-key shortcut.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="extra">Additional modifiers.</param>
        /// <returns>Key gesture.</returns>
        internal static KeyGesture Cmd(Key key, KeyModifiers extra = KeyModifiers.None)
        {
            return new KeyGesture(key, KeyModifiers.Meta | extra);
        }

        /// <summary>
        /// Open a URL in the default browser.
        /// </summary>
        /// <param name="url">URL.</param>
        internal static void OpenUrl(string url)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            Process? process = Process.Start(startInfo);
            process?.Dispose();
        }

        #endregion

        #region Private-Methods

        private static NativeMenuItem CreateWindowMenu(Window window)
        {
            NativeMenuItem windowMenu = CreateSubmenu("Window");
            NativeMenu menu = windowMenu.Menu!;
            PopulateWindowMenu(menu, window);
            menu.NeedsUpdate += delegate { PopulateWindowMenu(menu, window); };
            return windowMenu;
        }

        private static void PopulateWindowMenu(NativeMenu menu, Window window)
        {
            menu.Items.Clear();
            menu.Items.Add(CreateItem("Minimize", delegate { window.WindowState = WindowState.Minimized; }, Cmd(Key.M)));
            menu.Items.Add(CreateItem("Zoom", delegate
            {
                window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }));
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(CreateItem("Profiles", ShowMainWindow, Cmd(Key.P, KeyModifiers.Shift)));

            TerminalWorkspaceWindow[] workspaces = GetOpenWindows().OfType<TerminalWorkspaceWindow>().ToArray();
            if (workspaces.Length < 1) return;

            menu.Items.Add(new NativeMenuItemSeparator());
            foreach (TerminalWorkspaceWindow workspace in workspaces)
            {
                NativeMenuItem item = CreateItem(workspace.Title ?? workspace.ProfileName, delegate { BringToFront(workspace); });
                item.ToggleType = MenuItemToggleType.CheckBox;
                item.IsChecked = workspace == window;
                menu.Items.Add(item);
            }
        }

        private static NativeMenuItem CreateHelpMenu()
        {
            return CreateSubmenu("Help",
                CreateItem("Termrig on GitHub", delegate { OpenUrl(RepositoryUrl); }),
                CreateItem("Report an Issue", delegate { OpenUrl(IssuesUrl); }),
                CreateItem("Join the Discord Server", delegate { OpenUrl(DiscordUrl); }));
        }

        private static async Task ShowAboutAsync()
        {
            AboutWindow about = new AboutWindow();
            Window? owner = GetOpenWindows().FirstOrDefault(window => window.IsActive && window.IsVisible);
            if (owner == null)
            {
                about.Show();
                return;
            }

            await about.ShowDialog(owner).ConfigureAwait(true);
        }

        private static void ShowMainWindow()
        {
            if (Application.Current is App app) app.ShowMainWindow();
        }

        private static void BringToFront(Window window)
        {
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
        }

        private static Window[] GetOpenWindows()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.Windows.ToArray();
            }

            return Array.Empty<Window>();
        }

        private static TextBox? GetFocusedTextBox(Window window)
        {
            return window.FocusManager?.GetFocusedElement() as TextBox;
        }

        #endregion
    }
}
