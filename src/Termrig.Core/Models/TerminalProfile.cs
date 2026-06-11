namespace Termrig.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Saved collection of terminal tabs.
    /// </summary>
    public class TerminalProfile
    {
        #region Public-Members

        /// <summary>
        /// Profile identifier.
        /// </summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Profile display name.
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
        /// Default color scheme applied to tabs without an override.
        /// </summary>
        public ColorScheme GlobalColorScheme { get; set; } = new ColorScheme();

        /// <summary>
        /// True to open this profile automatically when Termrig starts.
        /// </summary>
        public bool AutoOpen { get; set; } = false;

        /// <summary>
        /// Optional profile folder identifier. Empty value means the profile is not in a folder.
        /// </summary>
        public string FolderId { get; set; } = String.Empty;

        /// <summary>
        /// Optional profile-wide terminal font family. Null means the system/default font is used.
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
        /// Optional profile-wide terminal font size. Null means the system/default font size is used. Minimum is 8 and maximum is 36.
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
        /// Tabs opened when the profile is launched.
        /// </summary>
        public List<TerminalTabProfile> Tabs { get; set; } = new List<TerminalTabProfile>();

        #endregion

        #region Private-Members

        private string _Id = Guid.NewGuid().ToString("N");
        private string _Name = "New Profile";
        private string? _FontFamily = null;
        private double? _FontSize = null;

        #endregion
    }
}
