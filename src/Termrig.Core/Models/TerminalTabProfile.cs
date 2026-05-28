namespace Termrig.Core.Models
{
    using System;
    using Termrig.Core.Enums;

    /// <summary>
    /// Saved configuration for a terminal tab.
    /// </summary>
    public class TerminalTabProfile
    {
        #region Public-Members

        /// <summary>
        /// Display name for the tab.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Shell type launched in this tab.
        /// </summary>
        public ShellType Shell { get; set; } = ShellType.PowerShell;

        /// <summary>
        /// Optional starting directory. Empty value means the current process directory.
        /// </summary>
        public string StartingDirectory { get; set; } = String.Empty;

        /// <summary>
        /// Optional startup script executed automatically when the tab opens.
        /// </summary>
        public string StartupScript { get; set; } = String.Empty;

        /// <summary>
        /// Optional color scheme override for this tab. Null means the profile scheme is used.
        /// </summary>
        public ColorScheme? ColorSchemeOverride { get; set; } = null;

        /// <summary>
        /// Terminal font family. Default is "Cascadia Mono,Consolas,Menlo,monospace".
        /// </summary>
        public string FontFamily
        {
            get
            {
                return _FontFamily;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(FontFamily));
                _FontFamily = value;
            }
        }

        /// <summary>
        /// Terminal font size. Default is 14. Minimum is 8 and maximum is 36.
        /// </summary>
        public double FontSize
        {
            get
            {
                return _FontSize;
            }
            set
            {
                _FontSize = Math.Clamp(value, 8, 36);
            }
        }

        #endregion

        #region Private-Members

        private string _Name = "Terminal";
        private string _FontFamily = "Cascadia Mono,Consolas,Menlo,monospace";
        private double _FontSize = 14;

        #endregion
    }
}
