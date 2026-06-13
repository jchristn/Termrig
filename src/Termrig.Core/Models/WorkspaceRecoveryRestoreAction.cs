namespace Termrig.Core.Models
{
    using System;

    /// <summary>
    /// A workspace recovery action matched to an existing profile.
    /// </summary>
    public class WorkspaceRecoveryRestoreAction
    {
        #region Public-Members

        /// <summary>
        /// Persisted workspace entry.
        /// </summary>
        public WorkspaceRecoveryWorkspace Workspace
        {
            get
            {
                return _Workspace;
            }
            set
            {
                _Workspace = value ?? throw new ArgumentNullException(nameof(Workspace));
            }
        }

        /// <summary>
        /// Profile to open for the workspace entry.
        /// </summary>
        public TerminalProfile Profile
        {
            get
            {
                return _Profile;
            }
            set
            {
                _Profile = value ?? throw new ArgumentNullException(nameof(Profile));
            }
        }

        #endregion

        #region Private-Members

        private WorkspaceRecoveryWorkspace _Workspace = new WorkspaceRecoveryWorkspace();
        private TerminalProfile _Profile = new TerminalProfile();

        #endregion
    }
}
