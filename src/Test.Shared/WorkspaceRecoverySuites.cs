namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Termrig.Core;
    using Termrig.Core.Models;
    using Termrig.Core.Services;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone test suites for crash recovery behavior.
    /// </summary>
    public static class WorkspaceRecoverySuites
    {
        /// <summary>
        /// Application crash log filename and content suite.
        /// </summary>
        /// <returns>Test suite descriptor.</returns>
        public static TestSuiteDescriptor ApplicationCrashLogStoreSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ApplicationCrashLogStore",
                displayName: "Application Crash Log Store",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "ApplicationCrashLogStore",
                        caseId: "WritesExactCrashFilename",
                        displayName: "Application crash log uses exact crash filename",
                        executeAsync: WritesExactCrashFilenameAsync),

                    new TestCaseDescriptor(
                        suiteId: "ApplicationCrashLogStore",
                        caseId: "IncludesRecoverySummary",
                        displayName: "Application crash log includes recovery summary",
                        executeAsync: IncludesRecoverySummaryAsync),

                    new TestCaseDescriptor(
                        suiteId: "ApplicationCrashLogStore",
                        caseId: "ApplicationCrashWriteHonorsCancellation",
                        displayName: "Application crash async write honors cancellation",
                        executeAsync: ApplicationCrashWriteHonorsCancellationAsync)
                });
        }

        /// <summary>
        /// Workspace recovery store persistence suite.
        /// </summary>
        /// <returns>Test suite descriptor.</returns>
        public static TestSuiteDescriptor WorkspaceRecoveryStoreSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "WorkspaceRecoveryStore",
                displayName: "Workspace Recovery Store",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryStore",
                        caseId: "RunStartedCreatesPendingState",
                        displayName: "Run start creates dirty recovery state",
                        executeAsync: RunStartedCreatesPendingStateAsync),

                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryStore",
                        caseId: "OpenCloseDuplicateWorkspaces",
                        displayName: "Open and close preserve duplicate workspace instances",
                        executeAsync: OpenCloseDuplicateWorkspacesAsync),

                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryStore",
                        caseId: "PendingCrashDetectionRules",
                        displayName: "Pending crash detection honors clean and handled flags",
                        executeAsync: PendingCrashDetectionRulesAsync),

                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryStore",
                        caseId: "MalformedJsonIsQuarantined",
                        displayName: "Malformed recovery JSON is quarantined",
                        executeAsync: MalformedJsonIsQuarantinedAsync),

                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryStore",
                        caseId: "NullWorkspaceListNormalizes",
                        displayName: "Null workspace list normalizes to empty",
                        executeAsync: NullWorkspaceListNormalizesAsync),

                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryStore",
                        caseId: "CancellationBeforeWriteThrows",
                        displayName: "Recovery store honors cancellation before write",
                        executeAsync: CancellationBeforeWriteThrowsAsync),

                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryStore",
                        caseId: "InvalidWorkspaceArgumentsThrow",
                        displayName: "Recovery store validates workspace arguments",
                        executeAsync: InvalidWorkspaceArgumentsThrowAsync)
                });
        }

        /// <summary>
        /// Workspace recovery restore planner suite.
        /// </summary>
        /// <returns>Test suite descriptor.</returns>
        public static TestSuiteDescriptor WorkspaceRecoveryPlannerSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "WorkspaceRecoveryPlanner",
                displayName: "Workspace Recovery Planner",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryPlanner",
                        caseId: "ProfileIdWinsOverName",
                        displayName: "Profile identifier wins over fallback name",
                        executeAsync: ProfileIdWinsOverNameAsync),

                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryPlanner",
                        caseId: "NameFallbackMatchesUniqueProfile",
                        displayName: "Name fallback matches one unique profile",
                        executeAsync: NameFallbackMatchesUniqueProfileAsync),

                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryPlanner",
                        caseId: "AmbiguousNameFallbackSkipsWorkspace",
                        displayName: "Ambiguous name fallback skips workspace",
                        executeAsync: AmbiguousNameFallbackSkipsWorkspaceAsync),

                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryPlanner",
                        caseId: "DuplicateWorkspaceEntriesArePreserved",
                        displayName: "Duplicate workspace entries produce duplicate restore actions",
                        executeAsync: DuplicateWorkspaceEntriesArePreservedAsync),

                    new TestCaseDescriptor(
                        suiteId: "WorkspaceRecoveryPlanner",
                        caseId: "MissingProfileIsSkipped",
                        displayName: "Missing profile is skipped",
                        executeAsync: MissingProfileIsSkippedAsync)
                });
        }

        private static async Task WritesExactCrashFilenameAsync(CancellationToken token)
        {
            string directory = BuildTempDirectory();
            try
            {
                CrashLogStore store = new CrashLogStore(directory);
                string path = await store.WriteApplicationCrashAsync("Boom", "Details", null, token).ConfigureAwait(false);
                string filename = Path.GetFileName(path);

                if (!Regex.IsMatch(filename, "^crash-[0-9]{8}-[0-9]{6}\\.log$"))
                {
                    throw new InvalidOperationException("Unexpected application crash filename: " + filename);
                }

                if (Path.GetDirectoryName(path) != directory)
                {
                    throw new InvalidOperationException("Application crash log was not written to the configured directory.");
                }
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        private static async Task IncludesRecoverySummaryAsync(CancellationToken token)
        {
            string directory = BuildTempDirectory();
            try
            {
                CrashLogStore store = new CrashLogStore(directory);
                WorkspaceRecoveryState state = new WorkspaceRecoveryState
                {
                    RunId = "run1",
                    CleanShutdown = false,
                    OpenWorkspaces = new List<WorkspaceRecoveryWorkspace>
                    {
                        new WorkspaceRecoveryWorkspace
                        {
                            WorkspaceId = "workspace1",
                            ProfileId = "profile1",
                            ProfileName = "Work"
                        }
                    }
                };

                string path = await store.WriteApplicationCrashAsync("Unhandled application exception.", "System.InvalidOperationException: Broken", state, token).ConfigureAwait(false);
                string contents = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);

                AssertContains(contents, "Application: Termrig", "Expected application name.");
                AssertContains(contents, "ProcessId: ", "Expected process identifier.");
                AssertContains(contents, "RecoveryRunId: run1", "Expected recovery run identifier.");
                AssertContains(contents, "RecoveryOpenWorkspaces: 1", "Expected recovery workspace count.");
                AssertContains(contents, "System.InvalidOperationException: Broken", "Expected exception details.");
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        private static async Task ApplicationCrashWriteHonorsCancellationAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string directory = BuildTempDirectory();
            try
            {
                CrashLogStore store = new CrashLogStore(directory);
                CancellationTokenSource source = new CancellationTokenSource();
                source.Cancel();

                try
                {
                    await store.WriteApplicationCrashAsync("Boom", "Details", null, source.Token).ConfigureAwait(false);
                    throw new InvalidOperationException("Expected OperationCanceledException.");
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    source.Dispose();
                }
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        private static async Task RunStartedCreatesPendingStateAsync(CancellationToken token)
        {
            string directory = BuildTempDirectory();
            try
            {
                WorkspaceRecoveryStore store = new WorkspaceRecoveryStore(directory);
                WorkspaceRecoveryState state = await store.MarkRunStartedAsync("run1", 123, token).ConfigureAwait(false);
                WorkspaceRecoveryState? loaded = await store.LoadAsync(token).ConfigureAwait(false);
                WorkspaceRecoveryState? pending = await store.GetPendingCrashAsync(token).ConfigureAwait(false);

                if (!File.Exists(store.FilePath)) throw new InvalidOperationException("Expected recovery file to exist.");
                AssertEqual(Constants.WorkspaceRecoveryFilename, Path.GetFileName(store.FilePath), "Recovery filename mismatch.");
                AssertEqual(1, state.SchemaVersion, "Schema version mismatch.");
                AssertEqual("run1", loaded?.RunId, "Run identifier mismatch.");
                AssertEqual(123, loaded?.ProcessId, "Process identifier mismatch.");
                AssertEqual(false, loaded?.CleanShutdown, "Expected dirty run state.");
                AssertEqual(0, loaded?.OpenWorkspaces.Count, "Expected no open workspaces.");
                if (pending != null) throw new InvalidOperationException("No pending crash should exist without open workspaces.");
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        private static async Task OpenCloseDuplicateWorkspacesAsync(CancellationToken token)
        {
            string directory = BuildTempDirectory();
            try
            {
                WorkspaceRecoveryStore store = new WorkspaceRecoveryStore(directory);
                await store.MarkRunStartedAsync("run1", 123, token).ConfigureAwait(false);
                await store.RegisterWorkspaceOpenedAsync("run1", BuildWorkspace("workspace1", "profile1", "Work"), token).ConfigureAwait(false);
                await store.RegisterWorkspaceOpenedAsync("run1", BuildWorkspace("workspace2", "profile1", "Work"), token).ConfigureAwait(false);

                WorkspaceRecoveryState? loaded = await store.LoadAsync(token).ConfigureAwait(false);
                AssertEqual(2, loaded?.OpenWorkspaces.Count, "Expected duplicate profile workspace instances.");

                await store.RegisterWorkspaceClosedAsync("run1", "workspace1", token).ConfigureAwait(false);
                WorkspaceRecoveryState? afterClose = await store.LoadAsync(token).ConfigureAwait(false);
                AssertEqual(1, afterClose?.OpenWorkspaces.Count, "Expected one remaining workspace.");
                AssertEqual("workspace2", afterClose?.OpenWorkspaces[0].WorkspaceId, "Expected second workspace to remain.");

                WorkspaceRecoveryState? pending = await store.GetPendingCrashAsync(token).ConfigureAwait(false);
                AssertEqual(1, pending?.OpenWorkspaces.Count, "Expected remaining workspace to be pending.");
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        private static async Task PendingCrashDetectionRulesAsync(CancellationToken token)
        {
            string directory = BuildTempDirectory();
            try
            {
                WorkspaceRecoveryStore store = new WorkspaceRecoveryStore(directory);
                await store.MarkRunStartedAsync("run1", 123, token).ConfigureAwait(false);
                await store.RegisterWorkspaceOpenedAsync("run1", BuildWorkspace("workspace1", "profile1", "Work"), token).ConfigureAwait(false);
                WorkspaceRecoveryState? pending = await store.GetPendingCrashAsync(token).ConfigureAwait(false);
                if (pending == null) throw new InvalidOperationException("Expected pending crash state.");

                await store.MarkRestorePromptHandledAsync(token).ConfigureAwait(false);
                WorkspaceRecoveryState? handled = await store.GetPendingCrashAsync(token).ConfigureAwait(false);
                if (handled != null) throw new InvalidOperationException("Handled prompt should suppress pending crash state.");

                await store.MarkRunStartedAsync("run2", 456, token).ConfigureAwait(false);
                await store.RegisterWorkspaceOpenedAsync("run2", BuildWorkspace("workspace2", "profile2", "Ops"), token).ConfigureAwait(false);
                await store.MarkCleanShutdownAsync("run2", token).ConfigureAwait(false);
                WorkspaceRecoveryState? clean = await store.GetPendingCrashAsync(token).ConfigureAwait(false);
                if (clean != null) throw new InvalidOperationException("Clean shutdown should suppress pending crash state.");
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        private static async Task MalformedJsonIsQuarantinedAsync(CancellationToken token)
        {
            string directory = BuildTempDirectory();
            try
            {
                Directory.CreateDirectory(directory);
                string filePath = Path.Combine(directory, Constants.WorkspaceRecoveryFilename);
                await File.WriteAllTextAsync(filePath, "{ not json", token).ConfigureAwait(false);

                WorkspaceRecoveryStore store = new WorkspaceRecoveryStore(directory);
                WorkspaceRecoveryState? state = await store.LoadAsync(token).ConfigureAwait(false);
                if (state != null) throw new InvalidOperationException("Malformed recovery JSON should be ignored.");
                if (File.Exists(filePath)) throw new InvalidOperationException("Malformed recovery JSON should be quarantined.");
                if (!File.Exists(filePath + ".bad")) throw new InvalidOperationException("Expected malformed recovery JSON quarantine file.");
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        private static async Task NullWorkspaceListNormalizesAsync(CancellationToken token)
        {
            string directory = BuildTempDirectory();
            try
            {
                Directory.CreateDirectory(directory);
                string filePath = Path.Combine(directory, Constants.WorkspaceRecoveryFilename);
                await File.WriteAllTextAsync(
                    filePath,
                    "{\"SchemaVersion\":1,\"RunId\":\"run1\",\"ProcessId\":123,\"CleanShutdown\":false,\"RestorePromptHandled\":false,\"OpenWorkspaces\":null}",
                    token).ConfigureAwait(false);

                WorkspaceRecoveryStore store = new WorkspaceRecoveryStore(directory);
                WorkspaceRecoveryState? state = await store.LoadAsync(token).ConfigureAwait(false);
                AssertEqual(0, state?.OpenWorkspaces.Count, "Expected null workspace list to normalize to empty.");

                WorkspaceRecoveryState? pending = await store.GetPendingCrashAsync(token).ConfigureAwait(false);
                if (pending != null) throw new InvalidOperationException("Empty workspace list should not be pending crash state.");
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        private static async Task CancellationBeforeWriteThrowsAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string directory = BuildTempDirectory();
            try
            {
                WorkspaceRecoveryStore store = new WorkspaceRecoveryStore(directory);
                CancellationTokenSource source = new CancellationTokenSource();
                source.Cancel();

                try
                {
                    await store.MarkRunStartedAsync("run1", 123, source.Token).ConfigureAwait(false);
                    throw new InvalidOperationException("Expected OperationCanceledException.");
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    source.Dispose();
                }
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        private static async Task InvalidWorkspaceArgumentsThrowAsync(CancellationToken token)
        {
            string directory = BuildTempDirectory();
            try
            {
                WorkspaceRecoveryStore store = new WorkspaceRecoveryStore(directory);
                await store.MarkRunStartedAsync("run1", 123, token).ConfigureAwait(false);

                try
                {
                    await store.RegisterWorkspaceOpenedAsync("run1", BuildWorkspace(String.Empty, "profile1", "Work"), token).ConfigureAwait(false);
                    throw new InvalidOperationException("Expected ArgumentNullException for empty workspace id.");
                }
                catch (ArgumentNullException)
                {
                }

                try
                {
                    await store.RegisterWorkspaceOpenedAsync("run1", BuildWorkspace("workspace1", String.Empty, "Work"), token).ConfigureAwait(false);
                    throw new InvalidOperationException("Expected ArgumentNullException for empty profile id.");
                }
                catch (ArgumentNullException)
                {
                }
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        private static Task ProfileIdWinsOverNameAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            WorkspaceRecoveryPlanner planner = new WorkspaceRecoveryPlanner();
            List<TerminalProfile> profiles = new List<TerminalProfile>
            {
                new TerminalProfile { Id = "profile-old-name", Name = "Old Name" },
                new TerminalProfile { Id = "profile-target", Name = "Current Name" }
            };
            WorkspaceRecoveryState state = BuildState(BuildWorkspace("workspace1", "profile-target", "Old Name"));

            WorkspaceRecoveryRestorePlan plan = planner.BuildRestorePlan(state, profiles);

            AssertEqual(1, plan.RestoreActions.Count, "Expected one restore action.");
            AssertEqual("profile-target", plan.RestoreActions[0].Profile.Id, "Expected profile id match to win over name.");
            AssertEqual(0, plan.SkippedWorkspaces.Count, "Expected no skipped workspaces.");
            return Task.CompletedTask;
        }

        private static Task NameFallbackMatchesUniqueProfileAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            WorkspaceRecoveryPlanner planner = new WorkspaceRecoveryPlanner();
            List<TerminalProfile> profiles = new List<TerminalProfile>
            {
                new TerminalProfile { Id = "profile1", Name = "Work" },
                new TerminalProfile { Id = "profile2", Name = "Ops" }
            };
            WorkspaceRecoveryState state = BuildState(BuildWorkspace("workspace1", "missing", "work"));

            WorkspaceRecoveryRestorePlan plan = planner.BuildRestorePlan(state, profiles);

            AssertEqual(1, plan.RestoreActions.Count, "Expected one restore action.");
            AssertEqual("profile1", plan.RestoreActions[0].Profile.Id, "Expected case-insensitive name fallback.");
            return Task.CompletedTask;
        }

        private static Task AmbiguousNameFallbackSkipsWorkspaceAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            WorkspaceRecoveryPlanner planner = new WorkspaceRecoveryPlanner();
            List<TerminalProfile> profiles = new List<TerminalProfile>
            {
                new TerminalProfile { Id = "profile1", Name = "Work" },
                new TerminalProfile { Id = "profile2", Name = "work" }
            };
            WorkspaceRecoveryState state = BuildState(BuildWorkspace("workspace1", "missing", "Work"));

            WorkspaceRecoveryRestorePlan plan = planner.BuildRestorePlan(state, profiles);

            AssertEqual(0, plan.RestoreActions.Count, "Expected no restore action for ambiguous name.");
            AssertEqual(1, plan.SkippedWorkspaces.Count, "Expected skipped workspace for ambiguous name.");
            return Task.CompletedTask;
        }

        private static Task DuplicateWorkspaceEntriesArePreservedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            WorkspaceRecoveryPlanner planner = new WorkspaceRecoveryPlanner();
            List<TerminalProfile> profiles = new List<TerminalProfile>
            {
                new TerminalProfile { Id = "profile1", Name = "Work" }
            };
            WorkspaceRecoveryState state = BuildState(
                BuildWorkspace("workspace1", "profile1", "Work"),
                BuildWorkspace("workspace2", "profile1", "Work"));

            WorkspaceRecoveryRestorePlan plan = planner.BuildRestorePlan(state, profiles);

            AssertEqual(2, plan.RestoreActions.Count, "Expected duplicate workspace restore actions.");
            AssertEqual("workspace1", plan.RestoreActions[0].Workspace.WorkspaceId, "First workspace id mismatch.");
            AssertEqual("workspace2", plan.RestoreActions[1].Workspace.WorkspaceId, "Second workspace id mismatch.");
            return Task.CompletedTask;
        }

        private static Task MissingProfileIsSkippedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            WorkspaceRecoveryPlanner planner = new WorkspaceRecoveryPlanner();
            List<TerminalProfile> profiles = new List<TerminalProfile>
            {
                new TerminalProfile { Id = "profile1", Name = "Work" }
            };
            WorkspaceRecoveryState state = BuildState(BuildWorkspace("workspace1", "missing", "Missing"));

            WorkspaceRecoveryRestorePlan plan = planner.BuildRestorePlan(state, profiles);

            AssertEqual(0, plan.RestoreActions.Count, "Expected no restore action for missing profile.");
            AssertEqual(1, plan.SkippedWorkspaces.Count, "Expected skipped workspace for missing profile.");
            return Task.CompletedTask;
        }

        private static WorkspaceRecoveryState BuildState(params WorkspaceRecoveryWorkspace[] workspaces)
        {
            return new WorkspaceRecoveryState
            {
                RunId = "run1",
                ProcessId = 123,
                CleanShutdown = false,
                RestorePromptHandled = false,
                OpenWorkspaces = workspaces.ToList()
            };
        }

        private static WorkspaceRecoveryWorkspace BuildWorkspace(string workspaceId, string profileId, string profileName)
        {
            return new WorkspaceRecoveryWorkspace
            {
                WorkspaceId = workspaceId,
                ProfileId = profileId,
                ProfileName = profileName,
                OpenedUtc = DateTime.UtcNow
            };
        }

        private static string BuildTempDirectory()
        {
            return Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"));
        }

        private static void DeleteTempDirectory(string directory)
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        private static void AssertContains(string contents, string expected, string message)
        {
            if (!contents.Contains(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(message + " Expected to find: " + expected + ".");
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException(message + " Expected: " + expected + "; Actual: " + actual + ".");
            }
        }
    }
}
