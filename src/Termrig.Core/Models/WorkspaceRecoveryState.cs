namespace Termrig.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Persisted state describing the current or most recent Termrig workspace run.
    /// </summary>
    public class WorkspaceRecoveryState
    {
        #region Public-Members

        /// <summary>
        /// Recovery file schema version. The current version is 1.
        /// </summary>
        public int SchemaVersion
        {
            get
            {
                return _SchemaVersion;
            }
            set
            {
                _SchemaVersion = Math.Max(1, value);
            }
        }

        /// <summary>
        /// Identifier for the Termrig process run that wrote this state.
        /// </summary>
        public string RunId
        {
            get
            {
                return _RunId;
            }
            set
            {
                _RunId = value ?? String.Empty;
            }
        }

        /// <summary>
        /// Operating system process identifier for the run that wrote this state.
        /// </summary>
        public int ProcessId
        {
            get
            {
                return _ProcessId;
            }
            set
            {
                _ProcessId = Math.Max(0, value);
            }
        }

        /// <summary>
        /// UTC time the run started.
        /// </summary>
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC time the state was last updated.
        /// </summary>
        public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the previous run completed a clean shutdown.
        /// </summary>
        public bool CleanShutdown { get; set; } = false;

        /// <summary>
        /// Whether the recovery prompt has already been handled for this state.
        /// </summary>
        public bool RestorePromptHandled { get; set; } = false;

        /// <summary>
        /// Workspaces open when this state was last written.
        /// </summary>
        public List<WorkspaceRecoveryWorkspace> OpenWorkspaces
        {
            get
            {
                return _OpenWorkspaces;
            }
            set
            {
                _OpenWorkspaces = value ?? new List<WorkspaceRecoveryWorkspace>();
            }
        }

        #endregion

        #region Private-Members

        private int _SchemaVersion = 1;
        private string _RunId = String.Empty;
        private int _ProcessId = 0;
        private List<WorkspaceRecoveryWorkspace> _OpenWorkspaces = new List<WorkspaceRecoveryWorkspace>();

        #endregion
    }
}
