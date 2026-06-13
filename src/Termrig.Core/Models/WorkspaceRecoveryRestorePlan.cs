namespace Termrig.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Result of matching recovery workspace entries against saved profiles.
    /// </summary>
    public class WorkspaceRecoveryRestorePlan
    {
        /// <summary>
        /// Workspace entries that can be restored.
        /// </summary>
        public List<WorkspaceRecoveryRestoreAction> RestoreActions { get; set; } = new List<WorkspaceRecoveryRestoreAction>();

        /// <summary>
        /// Workspace entries skipped because no unambiguous profile match exists.
        /// </summary>
        public List<WorkspaceRecoveryWorkspace> SkippedWorkspaces { get; set; } = new List<WorkspaceRecoveryWorkspace>();
    }
}
