namespace Termrig.Core.Models
{
    using System;

    /// <summary>
    /// Persisted runtime workspace instance used for crash recovery.
    /// </summary>
    public class WorkspaceRecoveryWorkspace
    {
        #region Public-Members

        /// <summary>
        /// Runtime workspace instance identifier.
        /// </summary>
        public string WorkspaceId
        {
            get
            {
                return _WorkspaceId;
            }
            set
            {
                _WorkspaceId = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Stable profile identifier for the opened workspace.
        /// </summary>
        public string ProfileId
        {
            get
            {
                return _ProfileId;
            }
            set
            {
                _ProfileId = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Profile display name captured as fallback metadata.
        /// </summary>
        public string ProfileName
        {
            get
            {
                return _ProfileName;
            }
            set
            {
                _ProfileName = value ?? String.Empty;
            }
        }

        /// <summary>
        /// UTC time the workspace was opened.
        /// </summary>
        public DateTime OpenedUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private string _WorkspaceId = String.Empty;
        private string _ProfileId = String.Empty;
        private string _ProfileName = String.Empty;

        #endregion
    }
}
