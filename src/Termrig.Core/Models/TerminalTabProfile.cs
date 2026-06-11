namespace Termrig.Core.Models
{
    using System;
    using Termrig.Core;
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
        public ShellType Shell { get; set; } = ShellType.Cmd;

        /// <summary>
        /// Shell display name.
        /// </summary>
        public string ShellDisplayName
        {
            get
            {
                if (Shell == ShellType.Cmd) return "cmd.exe";
                if (Shell == ShellType.Bash) return "bash";
                return "PowerShell";
            }
        }

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

        /// <summary>
        /// Optional per-tab terminal scrollback buffer size. Null means the application default is used.
        /// </summary>
        public int? ScrollbackBufferSize
        {
            get
            {
                return _ScrollbackBufferSize;
            }
            set
            {
                _ScrollbackBufferSize = value.HasValue
                    ? Math.Clamp(value.Value, Constants.MinimumTerminalBufferSize, Constants.MaximumTerminalBufferSize)
                    : null;
            }
        }

        /// <summary>
        /// True to record raw PTY output bytes for this tab.
        /// </summary>
        public bool RecordPtyOutput { get; set; } = false;

        /// <summary>
        /// Directory where raw PTY output recordings should be written when <see cref="RecordPtyOutput"/> is true.
        /// </summary>
        public string PtyRecordingDirectory { get; set; } = String.Empty;

        #endregion

        #region Private-Members

        private string _Name = "Terminal";
        private string? _FontFamily = null;
        private double? _FontSize = null;
        private int? _ScrollbackBufferSize = null;

        #endregion
    }
}
