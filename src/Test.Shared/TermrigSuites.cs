namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Termrig.Core.Enums;
    using Termrig.Core.Models;
    using Termrig.Core.Services;
    using Touchstone.Core;

    /// <summary>
    /// Touchstone test suite descriptors for Termrig.
    /// </summary>
    public static class TermrigSuites
    {
        /// <summary>
        /// All test suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    ProfileStoreSuite(),
                    ShellCatalogSuite()
                };
            }
        }

        /// <summary>
        /// Profile persistence suite.
        /// </summary>
        /// <returns>Test suite descriptor.</returns>
        public static TestSuiteDescriptor ProfileStoreSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ProfileStore",
                displayName: "Profile Store",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "ProfileStore",
                        caseId: "SaveAndLoadProfiles",
                        displayName: "Profiles persist and load from configured directory",
                        executeAsync: SaveAndLoadProfilesAsync),

                    new TestCaseDescriptor(
                        suiteId: "ProfileStore",
                        caseId: "UpsertReplacesExistingProfile",
                        displayName: "Upsert replaces existing profile",
                        executeAsync: UpsertReplacesExistingProfileAsync)
                });
        }

        /// <summary>
        /// Shell catalog suite.
        /// </summary>
        /// <returns>Test suite descriptor.</returns>
        public static TestSuiteDescriptor ShellCatalogSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ShellCatalog",
                displayName: "Shell Catalog",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "SupportedShellsAreDetected",
                        displayName: "Supported shells are detected",
                        executeAsync: SupportedShellsAreDetectedAsync),

                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "StartupScriptCreatesLaunchArguments",
                        displayName: "Startup scripts create launch arguments",
                        executeAsync: StartupScriptCreatesLaunchArgumentsAsync),

                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "WindowsSupportsCmdAndPowerShell",
                        displayName: "Windows supports cmd.exe and PowerShell",
                        executeAsync: WindowsSupportsCmdAndPowerShellAsync),

                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "WindowsShellExecutablesAreAbsolute",
                        displayName: "Windows shell executables are absolute paths",
                        executeAsync: WindowsShellExecutablesAreAbsoluteAsync),

                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "WindowsDirectoryPathUsesFileSystemCasing",
                        displayName: "Windows directory paths use filesystem casing",
                        executeAsync: WindowsDirectoryPathUsesFileSystemCasingAsync)
                });
        }

        private static async Task SaveAndLoadProfilesAsync(CancellationToken token)
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                ProfileStore store = new ProfileStore(directory);
                List<TerminalProfile> profiles = new List<TerminalProfile>
                {
                    new TerminalProfile
                    {
                        Name = "Work",
                        FontFamily = "Cascadia Code",
                        FontSize = 15,
                        Tabs = new List<TerminalTabProfile>
                        {
                            new TerminalTabProfile
                            {
                                Name = "PowerShell",
                                Shell = ShellType.PowerShell,
                                StartingDirectory = "C:\\Code",
                                StartupScript = "Write-Host ready",
                                FontFamily = "Consolas",
                                FontSize = 16
                            },
                            new TerminalTabProfile
                            {
                                Name = "Inherited",
                                Shell = ShellType.PowerShell,
                                StartingDirectory = "C:\\Code",
                                StartupScript = "Write-Host inherited"
                            }
                        }
                    }
                };

                await store.SaveAsync(profiles, token).ConfigureAwait(false);
                List<TerminalProfile> loaded = await store.LoadAsync(token).ConfigureAwait(false);

                AssertEqual(1, loaded.Count, "Expected one loaded profile.");
                AssertEqual("Work", loaded[0].Name, "Profile name mismatch.");
                AssertEqual("Cascadia Code", loaded[0].FontFamily, "Profile font family mismatch.");
                AssertEqual(15, loaded[0].FontSize, "Profile font size mismatch.");
                AssertEqual(2, loaded[0].Tabs.Count, "Expected two loaded tabs.");
                AssertEqual(ShellType.PowerShell, loaded[0].Tabs[0].Shell, "Shell mismatch.");
                AssertEqual("Write-Host ready", loaded[0].Tabs[0].StartupScript, "Startup script mismatch.");
                AssertEqual("Consolas", loaded[0].Tabs[0].FontFamily, "Font family mismatch.");
                AssertEqual(16, loaded[0].Tabs[0].FontSize, "Font size mismatch.");
                AssertEqual<string?>(null, loaded[0].Tabs[1].FontFamily, "Inherited tab font family should be null.");
                AssertEqual<double?>(null, loaded[0].Tabs[1].FontSize, "Inherited tab font size should be null.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static async Task UpsertReplacesExistingProfileAsync(CancellationToken token)
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                ProfileStore store = new ProfileStore(directory);
                TerminalProfile profile = new TerminalProfile { Id = "profile1", Name = "Old" };
                await store.UpsertAsync(profile, token).ConfigureAwait(false);

                profile.Name = "New";
                await store.UpsertAsync(profile, token).ConfigureAwait(false);

                List<TerminalProfile> loaded = await store.LoadAsync(token).ConfigureAwait(false);
                AssertEqual(1, loaded.Count, "Expected upsert to keep one profile.");
                AssertEqual("New", loaded[0].Name, "Upserted profile name mismatch.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static Task SupportedShellsAreDetectedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ShellCatalog catalog = new ShellCatalog();
            if (catalog.GetSupportedShells().Count < 1) throw new InvalidOperationException("Expected at least one supported shell.");
            return Task.CompletedTask;
        }

        private static Task StartupScriptCreatesLaunchArgumentsAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ShellCatalog catalog = new ShellCatalog();
            ShellDescriptor descriptor = catalog.GetSupportedShells()[0];
            TerminalTabProfile tab = new TerminalTabProfile
            {
                Name = "Test",
                Shell = descriptor.Shell,
                StartingDirectory = Environment.CurrentDirectory,
                StartupScript = "echo ready"
            };

            ShellLaunchPlan plan = catalog.BuildLaunchPlan(tab);

            AssertEqual(descriptor.Executable, plan.Executable, "Executable mismatch.");
            if (!plan.Arguments.Exists(item => item.Contains("echo ready", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Expected startup script in launch arguments.");
            }

            return Task.CompletedTask;
        }

        private static Task WindowsSupportsCmdAndPowerShellAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows()) return Task.CompletedTask;

            ShellCatalog catalog = new ShellCatalog();
            List<ShellDescriptor> shells = catalog.GetSupportedShells();
            if (!shells.Exists(item => item.Shell == ShellType.Cmd)) throw new InvalidOperationException("cmd.exe was not detected.");
            if (!shells.Exists(item => item.Shell == ShellType.PowerShell)) throw new InvalidOperationException("PowerShell was not detected.");
            return Task.CompletedTask;
        }

        private static Task WindowsDirectoryPathUsesFileSystemCasingAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows()) return Task.CompletedTask;

            string root = Path.Combine(Path.GetTempPath(), "TermrigCaseTest" + Guid.NewGuid().ToString("N"));
            string child = Path.Combine(root, "Code");
            try
            {
                Directory.CreateDirectory(child);
                string requested = Path.Combine(root.ToLowerInvariant(), "code");
                string normalized = ShellCatalog.NormalizeDirectoryPath(requested);
                if (!normalized.EndsWith("Code", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected normalized path to preserve filesystem casing. Actual: " + normalized);
                }
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }

            return Task.CompletedTask;
        }

        private static Task WindowsShellExecutablesAreAbsoluteAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows()) return Task.CompletedTask;

            ShellCatalog catalog = new ShellCatalog();
            List<ShellDescriptor> shells = catalog.GetSupportedShells();
            foreach (ShellDescriptor shell in shells)
            {
                if (!Path.IsPathFullyQualified(shell.Executable))
                {
                    throw new InvalidOperationException("Expected shell executable to be absolute. Shell: " + shell.Shell + "; Executable: " + shell.Executable + ".");
                }
            }

            return Task.CompletedTask;
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
