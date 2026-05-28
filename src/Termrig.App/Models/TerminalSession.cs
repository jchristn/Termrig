namespace Termrig.App.Models
{
    using Avalonia.Controls;
    using Iciclecreek.Terminal;
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
        /// Tab item displaying the terminal.
        /// </summary>
        public TabItem TabItem { get; set; } = new TabItem();

        /// <summary>
        /// Whether this runtime tab is already part of the saved profile.
        /// </summary>
        public bool IsProfileMember { get; set; } = true;

        /// <summary>
        /// Whether the terminal process is being closed by Termrig.
        /// </summary>
        public bool IsClosingByTermrig { get; set; } = false;
    }
}
