namespace Termrig.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Termrig.Core.Enums;
    using Termrig.Core.Models;

    /// <summary>
    /// Detects supported shells and builds launch plans for terminal tabs.
    /// </summary>
    public class ShellCatalog
    {
        #region Public-Methods

        /// <summary>
        /// Enumerate shells supported on the current host.
        /// </summary>
        /// <returns>Available shell descriptors.</returns>
        public List<ShellDescriptor> GetSupportedShells()
        {
            List<ShellDescriptor> shells = new List<ShellDescriptor>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                shells.Add(new ShellDescriptor { Shell = ShellType.Cmd, Name = "cmd.exe", Executable = ResolveCmdExecutable() });
                shells.Add(new ShellDescriptor { Shell = ShellType.PowerShell, Name = "PowerShell", Executable = ResolvePowerShellExecutable() });
            }
            else
            {
                string? bash = ResolveUnixExecutable("bash");
                if (!String.IsNullOrWhiteSpace(bash))
                {
                    shells.Add(new ShellDescriptor { Shell = ShellType.Bash, Name = "bash", Executable = bash });
                }
            }

            return shells;
        }

        /// <summary>
        /// Build shell process details for a tab.
        /// </summary>
        /// <param name="tab">Tab profile.</param>
        /// <returns>Launch plan.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tab is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the requested shell is not supported.</exception>
        public ShellLaunchPlan BuildLaunchPlan(TerminalTabProfile tab)
        {
            ArgumentNullException.ThrowIfNull(tab);

            ShellDescriptor? descriptor = GetSupportedShells().FirstOrDefault(item => item.Shell == tab.Shell);
            if (descriptor == null) throw new InvalidOperationException("Shell " + tab.Shell + " is not supported on this host.");

            ShellLaunchPlan plan = new ShellLaunchPlan
            {
                Executable = descriptor.Executable,
                StartingDirectory = NormalizeDirectoryPath(tab.StartingDirectory)
            };

            if (tab.Shell == ShellType.Cmd)
            {
                plan.Arguments.Add(String.IsNullOrWhiteSpace(tab.StartupScript) ? "/K" : "/K");
                if (!String.IsNullOrWhiteSpace(tab.StartupScript)) plan.Arguments.Add(tab.StartupScript);
            }
            else if (tab.Shell == ShellType.PowerShell)
            {
                plan.Arguments.Add("-NoLogo");
                plan.Arguments.Add("-NoProfile");
                plan.Arguments.Add("-NoExit");
                if (!String.IsNullOrWhiteSpace(tab.StartupScript))
                {
                    plan.Arguments.Add("-Command");
                    plan.Arguments.Add(tab.StartupScript);
                }
            }
            else if (tab.Shell == ShellType.Bash)
            {
                if (!String.IsNullOrWhiteSpace(tab.StartupScript))
                {
                    plan.Arguments.Add("-lc");
                    plan.Arguments.Add(tab.StartupScript + "; exec bash -i");
                }
                else
                {
                    plan.Arguments.Add("-i");
                }
            }

            return plan;
        }

        /// <summary>
        /// Resolve a directory to its absolute path using the host filesystem's path casing where possible.
        /// </summary>
        /// <param name="directory">Directory path.</param>
        /// <returns>Canonical directory path when it exists; otherwise the current directory.</returns>
        public static string NormalizeDirectoryPath(string directory)
        {
            string resolved = ResolveStartingDirectory(directory);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return resolved;

            string fullPath = Path.GetFullPath(resolved);
            string rootPath = Path.GetPathRoot(fullPath) ?? String.Empty;
            if (String.IsNullOrWhiteSpace(rootPath)) return fullPath;

            string normalizedRoot = rootPath.ToUpperInvariant();
            string relative = fullPath.Substring(rootPath.Length);
            if (String.IsNullOrWhiteSpace(relative)) return normalizedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            string current = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string[] parts = relative.Split(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(current);
                FileSystemInfo? match = directoryInfo.EnumerateFileSystemInfos()
                    .FirstOrDefault(item => String.Equals(item.Name, part, StringComparison.OrdinalIgnoreCase));
                current = Path.Combine(current, match == null ? part : match.Name);
            }

            return current;
        }

        #endregion

        #region Private-Methods

        private static string ResolveCmdExecutable()
        {
            string candidate = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            if (File.Exists(candidate)) return candidate;

            string? pathCandidate = ResolveWindowsExecutable("cmd.exe");
            if (!String.IsNullOrWhiteSpace(pathCandidate)) return pathCandidate;
            return "cmd.exe";
        }

        private static string ResolvePowerShellExecutable()
        {
            string? pwsh = ResolveWindowsExecutable("pwsh.exe");
            if (!String.IsNullOrWhiteSpace(pwsh)) return pwsh;

            string windowsPowerShell = Path.Combine(
                Environment.SystemDirectory,
                "WindowsPowerShell",
                "v1.0",
                "powershell.exe");
            if (File.Exists(windowsPowerShell)) return windowsPowerShell;

            string? powershell = ResolveWindowsExecutable("powershell.exe");
            if (!String.IsNullOrWhiteSpace(powershell)) return powershell;
            return "powershell.exe";
        }

        private static string? ResolveWindowsExecutable(string executable)
        {
            string? path = Environment.GetEnvironmentVariable("PATH");
            if (String.IsNullOrWhiteSpace(path)) return null;

            string[] directories = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            foreach (string directory in directories)
            {
                string candidate = Path.Combine(directory, executable);
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }

        private static string? ResolveUnixExecutable(string executable)
        {
            string[] candidates = new string[]
            {
                "/bin/" + executable,
                "/usr/bin/" + executable,
                "/usr/local/bin/" + executable
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static string ResolveStartingDirectory(string startingDirectory)
        {
            if (!String.IsNullOrWhiteSpace(startingDirectory) && Directory.Exists(startingDirectory)) return Path.GetFullPath(startingDirectory);
            return Environment.CurrentDirectory;
        }

        #endregion
    }
}
