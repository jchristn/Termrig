namespace Termrig.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Termrig.Core.Models;

    /// <summary>
    /// Builds restore actions from persisted workspace recovery state.
    /// </summary>
    public class WorkspaceRecoveryPlanner
    {
        #region Public-Methods

        /// <summary>
        /// Match persisted workspaces to saved profiles using profile identifier first and profile name as a fallback.
        /// </summary>
        /// <param name="state">Persisted recovery state.</param>
        /// <param name="profiles">Available saved profiles.</param>
        /// <returns>Restore plan containing matched and skipped workspace entries.</returns>
        /// <exception cref="ArgumentNullException">Thrown when state or profiles is null.</exception>
        public WorkspaceRecoveryRestorePlan BuildRestorePlan(WorkspaceRecoveryState state, List<TerminalProfile> profiles)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(profiles);

            WorkspaceRecoveryRestorePlan plan = new WorkspaceRecoveryRestorePlan();
            foreach (WorkspaceRecoveryWorkspace workspace in state.OpenWorkspaces)
            {
                TerminalProfile? profile = FindProfile(workspace, profiles);
                if (profile == null)
                {
                    plan.SkippedWorkspaces.Add(workspace);
                    continue;
                }

                plan.RestoreActions.Add(new WorkspaceRecoveryRestoreAction
                {
                    Workspace = workspace,
                    Profile = profile
                });
            }

            return plan;
        }

        #endregion

        #region Private-Methods

        private static TerminalProfile? FindProfile(WorkspaceRecoveryWorkspace workspace, List<TerminalProfile> profiles)
        {
            if (!String.IsNullOrWhiteSpace(workspace.ProfileId))
            {
                TerminalProfile? profile = profiles.FirstOrDefault(item => item.Id.Equals(workspace.ProfileId, StringComparison.OrdinalIgnoreCase));
                if (profile != null) return profile;
            }

            if (String.IsNullOrWhiteSpace(workspace.ProfileName)) return null;

            List<TerminalProfile> matches = profiles
                .Where(item => item.Name.Equals(workspace.ProfileName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return matches.Count == 1 ? matches[0] : null;
        }

        #endregion
    }
}
