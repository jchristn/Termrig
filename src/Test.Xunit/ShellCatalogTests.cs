namespace Test.Xunit
{
    using System;
    using System.IO;
    using System.Linq;
    using Termrig.Core.Enums;
    using Termrig.Core.Models;
    using Termrig.Core.Services;
    using Xunit;

    /// <summary>
    /// Tests for shell detection and launch-plan behavior.
    /// </summary>
    public class ShellCatalogTests
    {
        /// <summary>
        /// The catalog returns at least one shell supported by the current host.
        /// </summary>
        [Fact]
        public void SupportedShellsAreDetected()
        {
            ShellCatalog catalog = new ShellCatalog();
            Assert.NotEmpty(catalog.GetSupportedShells());
        }

        /// <summary>
        /// Startup scripts are converted into shell-specific launch arguments.
        /// </summary>
        [Fact]
        public void StartupScriptCreatesLaunchArguments()
        {
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

            Assert.Equal(descriptor.Executable, plan.Executable);
            Assert.Contains(plan.Arguments, item => item.Contains("echo ready", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Windows supports both MVP Windows shells.
        /// </summary>
        [Fact]
        public void WindowsSupportsCmdAndPowerShell()
        {
            if (!OperatingSystem.IsWindows()) return;

            ShellCatalog catalog = new ShellCatalog();
            Assert.Contains(catalog.GetSupportedShells(), item => item.Shell == ShellType.Cmd);
            Assert.Contains(catalog.GetSupportedShells(), item => item.Shell == ShellType.PowerShell);
        }

        /// <summary>
        /// Existing Windows directory paths are normalized to filesystem casing.
        /// </summary>
        [Fact]
        public void WindowsDirectoryPathUsesFileSystemCasing()
        {
            if (!OperatingSystem.IsWindows()) return;

            string root = Path.Combine(Path.GetTempPath(), "TermrigCaseTest" + Guid.NewGuid().ToString("N"));
            string child = Path.Combine(root, "Code");
            try
            {
                Directory.CreateDirectory(child);
                string requested = Path.Combine(root.ToLowerInvariant(), "code");
                string normalized = ShellCatalog.NormalizeDirectoryPath(requested);
                Assert.EndsWith("Code", normalized, StringComparison.Ordinal);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
