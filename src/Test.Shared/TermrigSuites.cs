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
                    CrashLogStoreSuite(),
                    ColorSchemeStoreSuite(),
                    ProfileStoreSuite(),
                    ProfileFolderStoreSuite(),
                    ShellCatalogSuite(),
                    XTermSuites.XTermNetRegressionSuite()
                };
            }
        }

        /// <summary>
        /// Crash log persistence suite.
        /// </summary>
        /// <returns>Test suite descriptor.</returns>
        public static TestSuiteDescriptor CrashLogStoreSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "CrashLogStore",
                displayName: "Crash Log Store",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "CrashLogStore",
                        caseId: "CreatesCrashDirectory",
                        displayName: "Crash log store creates crash directory",
                        executeAsync: CreatesCrashDirectoryAsync),

                    new TestCaseDescriptor(
                        suiteId: "CrashLogStore",
                        caseId: "WritesFormattedCrashLog",
                        displayName: "Crash log store writes formatted crash logs",
                        executeAsync: WritesFormattedCrashLogAsync)
                });
        }

        /// <summary>
        /// Color scheme persistence suite.
        /// </summary>
        /// <returns>Test suite descriptor.</returns>
        public static TestSuiteDescriptor ColorSchemeStoreSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ColorSchemeStore",
                displayName: "Color Scheme Store",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "ColorSchemeStore",
                        caseId: "MissingStoreLoadsDefaults",
                        displayName: "Missing color scheme store loads defaults",
                        executeAsync: MissingColorSchemeStoreLoadsDefaultsAsync),

                    new TestCaseDescriptor(
                        suiteId: "ColorSchemeStore",
                        caseId: "SaveAndLoadSchemes",
                        displayName: "Color schemes persist and load",
                        executeAsync: SaveAndLoadSchemesAsync),

                    new TestCaseDescriptor(
                        suiteId: "ColorSchemeStore",
                        caseId: "ResetDefaultsRestoresBuiltIns",
                        displayName: "Reset defaults restores built-in schemes",
                        executeAsync: ResetDefaultsRestoresBuiltInsAsync)
                });
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
                        executeAsync: UpsertReplacesExistingProfileAsync),

                    new TestCaseDescriptor(
                        suiteId: "ProfileStore",
                        caseId: "LoadNormalizesNullableProfileFields",
                        displayName: "Load normalizes nullable profile fields",
                        executeAsync: LoadNormalizesNullableProfileFieldsAsync),

                    new TestCaseDescriptor(
                        suiteId: "ProfileStore",
                        caseId: "TabScrollbackBufferSizeIsClamped",
                        displayName: "Tab scrollback buffer size is clamped",
                        executeAsync: TabScrollbackBufferSizeIsClampedAsync)
                });
        }

        /// <summary>
        /// Profile folder persistence suite.
        /// </summary>
        /// <returns>Test suite descriptor.</returns>
        public static TestSuiteDescriptor ProfileFolderStoreSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ProfileFolderStore",
                displayName: "Profile Folder Store",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "ProfileFolderStore",
                        caseId: "MissingFolderStoreLoadsEmpty",
                        displayName: "Missing profile folder store loads empty",
                        executeAsync: MissingFolderStoreLoadsEmptyAsync),

                    new TestCaseDescriptor(
                        suiteId: "ProfileFolderStore",
                        caseId: "SaveAndLoadProfileFolders",
                        displayName: "Profile folders persist and load",
                        executeAsync: SaveAndLoadProfileFoldersAsync)
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
                        caseId: "StartupScriptCreatesStartupCommands",
                        displayName: "Startup scripts create startup commands",
                        executeAsync: StartupScriptCreatesStartupCommandsAsync),

                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "LaunchArgumentsDoNotClearTerminal",
                        displayName: "Launch arguments do not clear the terminal",
                        executeAsync: LaunchArgumentsDoNotClearTerminalAsync),

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
                        caseId: "CmdLaunchUsesInteractiveArguments",
                        displayName: "cmd.exe launch uses interactive arguments",
                        executeAsync: CmdLaunchUsesInteractiveArgumentsAsync),

                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "MultilineStartupScriptsRunEachLine",
                        displayName: "Multiline startup scripts run each line",
                        executeAsync: MultilineStartupScriptsRunEachLineAsync),

                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "PowerShellLaunchUsesNoProfile",
                        displayName: "PowerShell launches without profile scripts",
                        executeAsync: PowerShellLaunchUsesNoProfileAsync),

                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "PowerShellStartupScriptsStayOutOfLaunchArguments",
                        displayName: "PowerShell startup scripts stay out of launch arguments",
                        executeAsync: PowerShellStartupScriptsStayOutOfLaunchArgumentsAsync),

                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "WindowsDirectoryPathUsesFileSystemCasing",
                        displayName: "Windows directory paths use filesystem casing",
                        executeAsync: WindowsDirectoryPathUsesFileSystemCasingAsync),

                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "TerminalTabProfileDefaultsToCmd",
                        displayName: "Terminal tab profiles default to cmd.exe",
                        executeAsync: TerminalTabProfileDefaultsToCmdAsync),

                    new TestCaseDescriptor(
                        suiteId: "ShellCatalog",
                        caseId: "ShellDisplayNamesAreUserFacing",
                        displayName: "Shell display names are user-facing",
                        executeAsync: ShellDisplayNamesAreUserFacingAsync)
                });
        }

        private static Task CreatesCrashDirectoryAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"), ".termrig", "crashes");
            try
            {
                CrashLogStore store = new CrashLogStore(directory);
                if (!Directory.Exists(store.DirectoryPath))
                {
                    throw new InvalidOperationException("Expected crash log directory to be created.");
                }

                return Task.CompletedTask;
            }
            finally
            {
                string root = Path.GetDirectoryName(Path.GetDirectoryName(directory) ?? String.Empty) ?? String.Empty;
                if (!String.IsNullOrWhiteSpace(root) && Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static async Task WritesFormattedCrashLogAsync(CancellationToken token)
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"), ".termrig", "crashes");
            try
            {
                CrashLogStore store = new CrashLogStore(directory);
                string path = await store.WriteAsync("Work Profile", "API/Tab", "Boom", "Details", token).ConfigureAwait(false);

                if (!File.Exists(path)) throw new InvalidOperationException("Expected crash log file to exist.");
                string filename = Path.GetFileName(path);
                if (!filename.EndsWith("-Work_Profile-APITab.log", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Unexpected crash log filename: " + filename);
                }

                string contents = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
                if (!contents.Contains("Summary: Boom", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Expected crash log summary.");
                }
            }
            finally
            {
                string root = Path.GetDirectoryName(Path.GetDirectoryName(directory) ?? String.Empty) ?? String.Empty;
                if (!String.IsNullOrWhiteSpace(root) && Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static async Task MissingColorSchemeStoreLoadsDefaultsAsync(CancellationToken token)
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                ColorSchemeStore store = new ColorSchemeStore(directory);
                List<ColorScheme> schemes = await store.LoadAsync(token).ConfigureAwait(false);

                AssertEqual(ColorSchemeCatalog.GetSchemes().Count, schemes.Count, "Expected default color schemes.");
                AssertEqual("Termrig Dark", schemes[0].Name, "Default scheme name mismatch.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static async Task SaveAndLoadSchemesAsync(CancellationToken token)
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                ColorSchemeStore store = new ColorSchemeStore(directory);
                List<ColorScheme> schemes = new List<ColorScheme>
                {
                    new ColorScheme
                    {
                        Name = "Custom",
                        Background = "#112233",
                        Foreground = "#DDEEFF"
                    }
                };

                await store.SaveAsync(schemes, token).ConfigureAwait(false);
                List<ColorScheme> loaded = await store.LoadAsync(token).ConfigureAwait(false);

                AssertEqual(1, loaded.Count, "Expected one loaded color scheme.");
                AssertEqual("Custom", loaded[0].Name, "Color scheme name mismatch.");
                AssertEqual("#112233", loaded[0].Background, "Color scheme background mismatch.");
                AssertEqual("#DDEEFF", loaded[0].Foreground, "Color scheme foreground mismatch.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static async Task ResetDefaultsRestoresBuiltInsAsync(CancellationToken token)
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                ColorSchemeStore store = new ColorSchemeStore(directory);
                await store.SaveAsync(new List<ColorScheme>
                {
                    new ColorScheme
                    {
                        Name = "Temporary",
                        Background = "#000000",
                        Foreground = "#FFFFFF"
                    }
                }, token).ConfigureAwait(false);

                List<ColorScheme> reset = await store.ResetDefaultsAsync(token).ConfigureAwait(false);
                List<ColorScheme> loaded = await store.LoadAsync(token).ConfigureAwait(false);

                AssertEqual(ColorSchemeCatalog.GetSchemes().Count, reset.Count, "Expected reset return count to match defaults.");
                AssertEqual(ColorSchemeCatalog.GetSchemes().Count, loaded.Count, "Expected loaded count to match defaults.");
                AssertEqual("Termrig Dark", loaded[0].Name, "Expected built-in default after reset.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
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
                        AutoOpen = true,
                        FolderId = "folder1",
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
                                FontSize = 16,
                                ScrollbackBufferSize = 7500
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
                AssertEqual(true, loaded[0].AutoOpen, "Profile auto-open mismatch.");
                AssertEqual("folder1", loaded[0].FolderId, "Profile folder id mismatch.");
                AssertEqual("Cascadia Code", loaded[0].FontFamily, "Profile font family mismatch.");
                AssertEqual(15, loaded[0].FontSize, "Profile font size mismatch.");
                AssertEqual(2, loaded[0].Tabs.Count, "Expected two loaded tabs.");
                AssertEqual(ShellType.PowerShell, loaded[0].Tabs[0].Shell, "Shell mismatch.");
                AssertEqual("Write-Host ready", loaded[0].Tabs[0].StartupScript, "Startup script mismatch.");
                AssertEqual("Consolas", loaded[0].Tabs[0].FontFamily, "Font family mismatch.");
                AssertEqual(16, loaded[0].Tabs[0].FontSize, "Font size mismatch.");
                AssertEqual(7500, loaded[0].Tabs[0].ScrollbackBufferSize, "Scrollback buffer size mismatch.");
                AssertEqual<string?>(null, loaded[0].Tabs[1].FontFamily, "Inherited tab font family should be null.");
                AssertEqual<double?>(null, loaded[0].Tabs[1].FontSize, "Inherited tab font size should be null.");
                AssertEqual<int?>(null, loaded[0].Tabs[1].ScrollbackBufferSize, "Default tab scrollback buffer size should be null.");
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

        private static async Task LoadNormalizesNullableProfileFieldsAsync(CancellationToken token)
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                string filePath = Path.Combine(directory, Termrig.Core.Constants.ProfilesFilename);
                await File.WriteAllTextAsync(
                    filePath,
                    "[{\"Id\":\"profile1\",\"Name\":\"Work\",\"GlobalColorScheme\":null,\"Tabs\":[{\"Name\":\"PowerShell\",\"Shell\":\"PowerShell\",\"StartingDirectory\":null,\"StartupScript\":null}]}]",
                    token).ConfigureAwait(false);

                ProfileStore store = new ProfileStore(directory);
                List<TerminalProfile> profiles = await store.LoadAsync(token).ConfigureAwait(false);

                AssertEqual(1, profiles.Count, "Expected one loaded profile.");
                if (profiles[0].GlobalColorScheme == null) throw new InvalidOperationException("Expected global color scheme to be normalized.");
                AssertEqual(false, profiles[0].AutoOpen, "Missing auto-open flag should default to false.");
                AssertEqual(String.Empty, profiles[0].FolderId, "Missing folder id should default to empty.");
                AssertEqual(1, profiles[0].Tabs.Count, "Expected one loaded tab.");
                AssertEqual(String.Empty, profiles[0].Tabs[0].StartingDirectory, "Expected null starting directory to be normalized.");
                AssertEqual(String.Empty, profiles[0].Tabs[0].StartupScript, "Expected null startup script to be normalized.");
                AssertEqual<int?>(null, profiles[0].Tabs[0].ScrollbackBufferSize, "Missing scrollback buffer size should remain null.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static async Task MissingFolderStoreLoadsEmptyAsync(CancellationToken token)
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                ProfileFolderStore store = new ProfileFolderStore(directory);
                List<ProfileFolder> folders = await store.LoadAsync(token).ConfigureAwait(false);
                AssertEqual(0, folders.Count, "Expected missing profile folder store to load empty.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static async Task SaveAndLoadProfileFoldersAsync(CancellationToken token)
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                ProfileFolderStore store = new ProfileFolderStore(directory);
                List<ProfileFolder> folders = new List<ProfileFolder>
                {
                    new ProfileFolder
                    {
                        Id = "folder1",
                        Name = "My Apps",
                        IsExpanded = false
                    }
                };

                await store.SaveAsync(folders, token).ConfigureAwait(false);
                List<ProfileFolder> loaded = await store.LoadAsync(token).ConfigureAwait(false);

                AssertEqual(1, loaded.Count, "Expected one loaded profile folder.");
                AssertEqual("folder1", loaded[0].Id, "Profile folder id mismatch.");
                AssertEqual("My Apps", loaded[0].Name, "Profile folder name mismatch.");
                AssertEqual(false, loaded[0].IsExpanded, "Profile folder expansion state mismatch.");
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static Task TabScrollbackBufferSizeIsClampedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            TerminalTabProfile tab = new TerminalTabProfile
            {
                ScrollbackBufferSize = 50
            };
            AssertEqual(Termrig.Core.Constants.MinimumTerminalBufferSize, tab.ScrollbackBufferSize, "Expected low scrollback buffer size to be clamped.");

            tab.ScrollbackBufferSize = 200000;
            AssertEqual(Termrig.Core.Constants.MaximumTerminalBufferSize, tab.ScrollbackBufferSize, "Expected high scrollback buffer size to be clamped.");

            tab.ScrollbackBufferSize = null;
            AssertEqual<int?>(null, tab.ScrollbackBufferSize, "Expected null scrollback buffer size to remain null.");
            return Task.CompletedTask;
        }

        private static Task SupportedShellsAreDetectedAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ShellCatalog catalog = new ShellCatalog();
            if (catalog.GetSupportedShells().Count < 1) throw new InvalidOperationException("Expected at least one supported shell.");
            return Task.CompletedTask;
        }

        private static Task StartupScriptCreatesStartupCommandsAsync(CancellationToken token)
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
            AssertEqual(1, plan.StartupCommands.Count, "Expected one startup command.");
            AssertEqual("echo ready", plan.StartupCommands[0], "Startup command mismatch.");

            return Task.CompletedTask;
        }

        private static Task LaunchArgumentsDoNotClearTerminalAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ShellCatalog catalog = new ShellCatalog();
            foreach (ShellDescriptor descriptor in catalog.GetSupportedShells())
            {
                ShellLaunchPlan plan = catalog.BuildLaunchPlan(new TerminalTabProfile
                {
                    Name = "Test",
                    Shell = descriptor.Shell,
                    StartingDirectory = Environment.CurrentDirectory,
                    StartupScript = "echo ready"
                });

                foreach (string argument in plan.Arguments)
                {
                    string normalized = argument.Trim().ToLowerInvariant();
                    if (normalized == "clear" || normalized == "cls" || normalized.Contains("clear-host", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Launch arguments should not clear the terminal. Argument: " + argument);
                    }
                }
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

        private static Task CmdLaunchUsesInteractiveArgumentsAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows()) return Task.CompletedTask;

            ShellCatalog catalog = new ShellCatalog();
            ShellLaunchPlan plan = catalog.BuildLaunchPlan(new TerminalTabProfile
            {
                Name = "cmd.exe",
                Shell = ShellType.Cmd,
                StartingDirectory = Environment.CurrentDirectory,
                StartupScript = "dir"
            });

            AssertEqual(2, plan.Arguments.Count, "Expected two cmd.exe launch arguments.");
            AssertEqual("/D", plan.Arguments[0], "Expected cmd.exe autorun suppression argument.");
            AssertEqual("/K", plan.Arguments[1], "Expected cmd.exe interactive keep-open argument.");
            AssertEqual(1, plan.StartupCommands.Count, "Expected cmd.exe startup command to be sent through the PTY.");
            AssertEqual("dir", plan.StartupCommands[0], "cmd.exe startup command mismatch.");
            return Task.CompletedTask;
        }

        private static Task MultilineStartupScriptsRunEachLineAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ShellCatalog catalog = new ShellCatalog();
            foreach (ShellDescriptor descriptor in catalog.GetSupportedShells())
            {
                ShellLaunchPlan plan = catalog.BuildLaunchPlan(new TerminalTabProfile
                {
                    Name = "Test",
                    Shell = descriptor.Shell,
                    StartingDirectory = Environment.CurrentDirectory,
                    StartupScript = "first command" + Environment.NewLine + Environment.NewLine + "second command"
                });

                AssertEqual(2, plan.StartupCommands.Count, "Expected two startup commands for " + descriptor.Shell + ".");
                AssertEqual("first command", plan.StartupCommands[0], "First startup command mismatch for " + descriptor.Shell + ".");
                AssertEqual("second command", plan.StartupCommands[1], "Second startup command mismatch for " + descriptor.Shell + ".");
            }

            return Task.CompletedTask;
        }

        private static Task PowerShellLaunchUsesNoProfileAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows()) return Task.CompletedTask;

            ShellCatalog catalog = new ShellCatalog();
            ShellLaunchPlan plan = catalog.BuildLaunchPlan(new TerminalTabProfile
            {
                Name = "PowerShell",
                Shell = ShellType.PowerShell,
                StartingDirectory = Environment.CurrentDirectory
            });

            if (!plan.Arguments.Exists(item => item.Equals("-NoProfile", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Expected PowerShell launch arguments to include -NoProfile.");
            }

            return Task.CompletedTask;
        }

        private static Task PowerShellStartupScriptsStayOutOfLaunchArgumentsAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!OperatingSystem.IsWindows()) return Task.CompletedTask;

            ShellCatalog catalog = new ShellCatalog();
            ShellLaunchPlan plan = catalog.BuildLaunchPlan(new TerminalTabProfile
            {
                Name = "PowerShell",
                Shell = ShellType.PowerShell,
                StartingDirectory = Environment.CurrentDirectory,
                StartupScript = "Write-Host ready"
            });

            if (plan.Arguments.Exists(item => item.Equals("-Command", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("PowerShell startup commands should be sent through the PTY, not launched with -Command.");
            }

            AssertEqual(1, plan.StartupCommands.Count, "Expected one PowerShell startup command.");
            AssertEqual("Write-Host ready", plan.StartupCommands[0], "PowerShell startup command mismatch.");
            return Task.CompletedTask;
        }

        private static Task ShellDisplayNamesAreUserFacingAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            AssertEqual("cmd.exe", new TerminalTabProfile { Shell = ShellType.Cmd }.ShellDisplayName, "cmd.exe display name mismatch.");
            AssertEqual("PowerShell", new TerminalTabProfile { Shell = ShellType.PowerShell }.ShellDisplayName, "PowerShell display name mismatch.");
            AssertEqual("bash", new TerminalTabProfile { Shell = ShellType.Bash }.ShellDisplayName, "bash display name mismatch.");
            return Task.CompletedTask;
        }

        private static Task TerminalTabProfileDefaultsToCmdAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            AssertEqual(ShellType.Cmd, new TerminalTabProfile().Shell, "Terminal tab profile default shell mismatch.");
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
