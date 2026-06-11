namespace Termrig.Core.Models
{
    using System;

    /// <summary>
    /// Saved folder used to organize terminal profiles.
    /// </summary>
    public class ProfileFolder
    {
        #region Public-Members

        /// <summary>
        /// Folder identifier.
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
        /// Folder display name.
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
        /// True when the folder is expanded in the profile list.
        /// </summary>
        public bool IsExpanded { get; set; } = true;

        #endregion

        #region Private-Members

        private string _Id = Guid.NewGuid().ToString("N");
        private string _Name = "New Folder";

        #endregion
    }
}
