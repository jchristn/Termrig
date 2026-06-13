namespace Termrig.Core.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Termrig.Core.Models;

    /// <summary>
    /// Stores crash recovery workspace state under ~/.termrig.
    /// </summary>
    public class WorkspaceRecoveryStore
    {
        #region Public-Members

        /// <summary>
        /// Directory containing Termrig recovery data.
        /// </summary>
        public string DirectoryPath
        {
            get
            {
                return _DirectoryPath;
            }
        }

        /// <summary>
        /// Full recovery JSON file path.
        /// </summary>
        public string FilePath
        {
            get
            {
                return Path.Combine(_DirectoryPath, Constants.WorkspaceRecoveryFilename);
            }
        }

        #endregion

        #region Private-Members

        private readonly string _DirectoryPath;
        private readonly JsonSerializerOptions _JsonOptions;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the workspace recovery store.
        /// </summary>
        /// <param name="directoryPath">Optional storage directory. Defaults to ~/.termrig.</param>
        public WorkspaceRecoveryStore(string? directoryPath = null)
        {
            _DirectoryPath = ResolveDirectoryPath(directoryPath);
            _JsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Load the persisted recovery state, or null when no usable file exists.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Recovery state, or null.</returns>
        public async Task<WorkspaceRecoveryState?> LoadAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(FilePath)) return null;

            try
            {
                using (FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    WorkspaceRecoveryState? state = await JsonSerializer.DeserializeAsync<WorkspaceRecoveryState>(
                        stream,
                        _JsonOptions,
                        token).ConfigureAwait(false);
                    return NormalizeState(state);
                }
            }
            catch (JsonException)
            {
                QuarantineInvalidFile();
                return null;
            }
            catch (NotSupportedException)
            {
                QuarantineInvalidFile();
                return null;
            }
        }

        /// <summary>
        /// Save recovery state atomically.
        /// </summary>
        /// <param name="state">Recovery state.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when state is null.</exception>
        public async Task SaveAsync(WorkspaceRecoveryState state, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(state);
            token.ThrowIfCancellationRequested();

            Directory.CreateDirectory(_DirectoryPath);
            NormalizeState(state);
            state.LastUpdatedUtc = DateTime.UtcNow;

            string tempPath = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(stream, state, _JsonOptions, token).ConfigureAwait(false);
                }

                token.ThrowIfCancellationRequested();
                File.Move(tempPath, FilePath, true);
            }
            finally
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
        }

        /// <summary>
        /// Start a new unclean run state.
        /// </summary>
        /// <param name="runId">Run identifier.</param>
        /// <param name="processId">Process identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created run state.</returns>
        /// <exception cref="ArgumentNullException">Thrown when runId is null or empty.</exception>
        public async Task<WorkspaceRecoveryState> MarkRunStartedAsync(string runId, int processId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(runId)) throw new ArgumentNullException(nameof(runId));
            token.ThrowIfCancellationRequested();

            DateTime now = DateTime.UtcNow;
            WorkspaceRecoveryState state = new WorkspaceRecoveryState
            {
                SchemaVersion = 1,
                RunId = runId,
                ProcessId = processId,
                StartedUtc = now,
                LastUpdatedUtc = now,
                CleanShutdown = false,
                RestorePromptHandled = false
            };
            await SaveAsync(state, token).ConfigureAwait(false);
            return state;
        }

        /// <summary>
        /// Register an opened workspace for the current run.
        /// </summary>
        /// <param name="runId">Run identifier.</param>
        /// <param name="workspace">Workspace entry.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when required arguments are null or empty.</exception>
        public async Task RegisterWorkspaceOpenedAsync(string runId, WorkspaceRecoveryWorkspace workspace, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(runId)) throw new ArgumentNullException(nameof(runId));
            ArgumentNullException.ThrowIfNull(workspace);
            if (String.IsNullOrWhiteSpace(workspace.WorkspaceId)) throw new ArgumentNullException(nameof(workspace.WorkspaceId));
            if (String.IsNullOrWhiteSpace(workspace.ProfileId)) throw new ArgumentNullException(nameof(workspace.ProfileId));
            token.ThrowIfCancellationRequested();

            WorkspaceRecoveryState state = await LoadCurrentRunOrCreateAsync(runId, token).ConfigureAwait(false);
            state.CleanShutdown = false;
            state.RestorePromptHandled = false;
            if (!state.OpenWorkspaces.Any(item => item.WorkspaceId.Equals(workspace.WorkspaceId, StringComparison.OrdinalIgnoreCase)))
            {
                state.OpenWorkspaces.Add(workspace);
            }

            await SaveAsync(state, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Register a cleanly closed workspace for the current run.
        /// </summary>
        /// <param name="runId">Run identifier.</param>
        /// <param name="workspaceId">Workspace identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when required arguments are null or empty.</exception>
        public async Task RegisterWorkspaceClosedAsync(string runId, string workspaceId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(runId)) throw new ArgumentNullException(nameof(runId));
            if (String.IsNullOrWhiteSpace(workspaceId)) throw new ArgumentNullException(nameof(workspaceId));
            token.ThrowIfCancellationRequested();

            WorkspaceRecoveryState? state = await LoadAsync(token).ConfigureAwait(false);
            if (state == null) return;
            if (!state.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase)) return;

            state.OpenWorkspaces.RemoveAll(item => item.WorkspaceId.Equals(workspaceId, StringComparison.OrdinalIgnoreCase));
            await SaveAsync(state, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Mark the current run as a clean shutdown.
        /// </summary>
        /// <param name="runId">Run identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when runId is null or empty.</exception>
        public async Task MarkCleanShutdownAsync(string runId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(runId)) throw new ArgumentNullException(nameof(runId));
            token.ThrowIfCancellationRequested();

            WorkspaceRecoveryState? state = await LoadAsync(token).ConfigureAwait(false);
            if (state == null) return;
            if (!state.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase)) return;

            state.CleanShutdown = true;
            state.RestorePromptHandled = true;
            state.OpenWorkspaces.Clear();
            await SaveAsync(state, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Mark the persisted restore prompt as already handled.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public async Task MarkRestorePromptHandledAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            WorkspaceRecoveryState? state = await LoadAsync(token).ConfigureAwait(false);
            if (state == null) return;

            state.RestorePromptHandled = true;
            await SaveAsync(state, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Load pending crash state if a previous run ended uncleanly with open workspaces.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Pending crash state, or null.</returns>
        public async Task<WorkspaceRecoveryState?> GetPendingCrashAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            WorkspaceRecoveryState? state = await LoadAsync(token).ConfigureAwait(false);
            if (state == null) return null;
            if (state.CleanShutdown) return null;
            if (state.RestorePromptHandled) return null;
            if (state.OpenWorkspaces.Count < 1) return null;
            return state;
        }

        #endregion

        #region Private-Methods

        private async Task<WorkspaceRecoveryState> LoadCurrentRunOrCreateAsync(string runId, CancellationToken token)
        {
            WorkspaceRecoveryState? state = await LoadAsync(token).ConfigureAwait(false);
            if (state != null && state.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase)) return state;

            DateTime now = DateTime.UtcNow;
            return new WorkspaceRecoveryState
            {
                SchemaVersion = 1,
                RunId = runId,
                ProcessId = Environment.ProcessId,
                StartedUtc = now,
                LastUpdatedUtc = now,
                CleanShutdown = false,
                RestorePromptHandled = false
            };
        }

        private WorkspaceRecoveryState? NormalizeState(WorkspaceRecoveryState? state)
        {
            if (state == null) return null;

            state.SchemaVersion = state.SchemaVersion;
            state.RunId ??= String.Empty;
            state.OpenWorkspaces = state.OpenWorkspaces
                .Where(item => item != null && !String.IsNullOrWhiteSpace(item.WorkspaceId))
                .ToList();

            if (state.StartedUtc.Kind != DateTimeKind.Utc) state.StartedUtc = DateTime.SpecifyKind(state.StartedUtc, DateTimeKind.Utc);
            if (state.LastUpdatedUtc.Kind != DateTimeKind.Utc) state.LastUpdatedUtc = DateTime.SpecifyKind(state.LastUpdatedUtc, DateTimeKind.Utc);

            foreach (WorkspaceRecoveryWorkspace workspace in state.OpenWorkspaces)
            {
                workspace.WorkspaceId ??= String.Empty;
                workspace.ProfileId ??= String.Empty;
                workspace.ProfileName ??= String.Empty;
                if (workspace.OpenedUtc.Kind != DateTimeKind.Utc) workspace.OpenedUtc = DateTime.SpecifyKind(workspace.OpenedUtc, DateTimeKind.Utc);
            }

            return state;
        }

        private void QuarantineInvalidFile()
        {
            if (!File.Exists(FilePath)) return;

            string badPath = FilePath + ".bad";
            File.Move(FilePath, badPath, true);
        }

        private static string ResolveDirectoryPath(string? directoryPath)
        {
            if (!String.IsNullOrWhiteSpace(directoryPath)) return directoryPath;

            string? home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (String.IsNullOrWhiteSpace(home)) throw new InvalidOperationException("Unable to resolve the current user's home directory.");
            return Path.Combine(home, Constants.ApplicationDirectoryName);
        }

        #endregion
    }
}
