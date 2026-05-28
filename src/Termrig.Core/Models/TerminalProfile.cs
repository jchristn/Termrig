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
        /// Tabs opened when the profile is launched.
        /// </summary>
        public List<TerminalTabProfile> Tabs { get; set; } = new List<TerminalTabProfile>();

        #endregion

        #region Private-Members

        private string _Id = Guid.NewGuid().ToString("N");
        private string _Name = "New Profile";

        #endregion
    }
}
