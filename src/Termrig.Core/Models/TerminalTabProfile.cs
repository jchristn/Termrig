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
        /// Optional terminal font family override. Null means the profile font is used.
        /// </summary>
        public string? FontFamily
        {
            get
            {
                return _FontFamily;
            }
            set
            {
                if (value != null && String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(FontFamily));
                _FontFamily = value;
            }
        }

        /// <summary>
        /// Optional terminal font size override. Null means the profile font size is used. Minimum is 8 and maximum is 36.
        /// </summary>
        public double? FontSize
        {
            get
            {
                return _FontSize;
            }
            set
            {
                _FontSize = value.HasValue ? Math.Clamp(value.Value, 8, 36) : null;
            }
        }

        #endregion

        #region Private-Members

        private string _Name = "Terminal";
        private string? _FontFamily = null;
        private double? _FontSize = null;

        #endregion
    }
}
