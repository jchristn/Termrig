namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Input;
    using System;
    using System.Reflection;
    using Termrig.App.Services;

    /// <summary>
    /// About dialog shown from the macOS application menu.
    /// </summary>
    public partial class AboutWindow : Window
    {
        /// <summary>
        /// Instantiate the about dialog.
        /// </summary>
        public AboutWindow()
        {
            InitializeComponent();
            VersionText.Text = "Version " + GetVersion();
            GitHubButton.Click += delegate { MacOSMenu.OpenUrl(MacOSMenu.RepositoryUrl); };
            DiscordButton.Click += delegate { MacOSMenu.OpenUrl(MacOSMenu.DiscordUrl); };
            CloseButton.Click += delegate { Close(); };
            KeyDown += OnKeyDown;
        }

        private static string GetVersion()
        {
            Assembly assembly = typeof(AboutWindow).Assembly;
            string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (String.IsNullOrWhiteSpace(version)) version = assembly.GetName().Version?.ToString(3) ?? "unknown";

            // Strip the "+<commit>" source revision suffix the SDK appends.
            int plus = version.IndexOf('+');
            return plus > 0 ? version.Substring(0, plus) : version;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            Close();
        }
    }
}
