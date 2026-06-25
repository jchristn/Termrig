namespace Termrig.App.Models
{
    using Avalonia.Controls;
    using Iciclecreek.Terminal;
    using System.Threading;
    using Termrig.Core.Models;

    /// <summary>
    /// Runtime terminal session associated with a saved tab profile.
    /// </summary>
    public class TerminalSession
    {
        /// <summary>
        /// Saved tab configuration.
        /// </summary>
        public TerminalTabProfile TabProfile { get; set; } = new TerminalTabProfile();

        /// <summary>
        /// Terminal control hosting the PTY process.
        /// </summary>
        public TerminalControl Terminal { get; set; } = new TerminalControl();

        /// <summary>
        /// Header control representing the terminal tab.
        /// </summary>
        public Control Header { get; set; } = new Border();

        /// <summary>
        /// Initial runtime font size used by reset zoom.
        /// </summary>
        public double RuntimeDefaultFontSize { get; set; } = 12;

        /// <summary>
        /// Whether this runtime tab is already part of the saved profile.
        /// </summary>
        public bool IsProfileMember { get; set; } = true;

        /// <summary>
        /// Whether the terminal process is being closed by Termrig.
        /// </summary>
        public bool IsClosingByTermrig { get; set; } = false;

        /// <summary>
        /// Launch plan for the terminal process.
        /// </summary>
        public ShellLaunchPlan LaunchPlan { get; set; } = new ShellLaunchPlan { Executable = "cmd.exe" };

        /// <summary>
        /// Whether launch has been requested for this tab.
        /// </summary>
        public bool IsLaunchRequested { get; set; } = false;

        /// <summary>
        /// Whether this tab has requested user attention.
        /// </summary>
        public bool HasAttention { get; set; } = false;

        /// <summary>
        /// True when the configured start directory was edited after launch and should not be replaced by the live process directory.
        /// </summary>
        public bool PreserveConfiguredStartingDirectory { get; set; } = false;

        /// <summary>
        /// Pending debounced scrollback restore save.
        /// </summary>
        public CancellationTokenSource? RestoreSaveDebounce { get; set; } = null;
    }
}
