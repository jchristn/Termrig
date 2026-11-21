namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Finds packaging tools, explains how to install missing ones, and runs them.</summary>
    public static class ToolRunner
    {
        private static readonly Dictionary<string, string> _InstallHints = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["dotnet"] = "Install the .NET SDK named in global.json from https://dotnet.microsoft.com/download.",
            ["iscc"] = "Install Inno Setup 6.3 or later: choco install innosetup --no-progress -y",
            ["signtool"] = "Install the Windows SDK signing tools, or set signing.windows.isConfigured to false.",
            ["codesign"] = "Install the Xcode command-line tools: xcode-select --install",
            ["pkgbuild"] = "Install the Xcode command-line tools: xcode-select --install",
            ["productbuild"] = "Install the Xcode command-line tools: xcode-select --install",
            ["productsign"] = "Install the Xcode command-line tools: xcode-select --install",
            ["hdiutil"] = "hdiutil ships with macOS; run this channel on a Mac.",
            ["ditto"] = "ditto ships with macOS; run this channel on a Mac.",
            ["xcrun"] = "Install the Xcode command-line tools: xcode-select --install",
            ["fpm"] = "Install fpm: sudo gem install --no-document fpm (needs Ruby; on Debian or Ubuntu: sudo apt-get install -y ruby ruby-dev build-essential)",
            ["rpmbuild"] = "Install rpmbuild: sudo DEBIAN_FRONTEND=noninteractive apt-get install -y rpm",
            ["appimagetool"] = "Download appimagetool from https://github.com/AppImage/appimagetool/releases/tag/continuous, chmod +x it, and put it on PATH or set APPIMAGETOOL."
        };

        /// <summary>Returns the install command or instructions for a tool, for preflight messages.</summary>
        /// <param name="tool">Tool name, for example <c>fpm</c>.</param>
        /// <returns>The hint, or a generic message for unknown tools.</returns>
        public static string InstallHint(string tool) => _InstallHints.TryGetValue(tool, out string? hint) ? hint : "Install " + tool + " and put it on PATH.";

        /// <summary>Finds a tool on PATH, then in its conventional install locations.</summary>
        /// <param name="tool">Tool name without extension.</param>
        /// <returns>The absolute path, or null when absent.</returns>
        public static string? Find(string tool)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tool);
            string? overrideVariable = Environment.GetEnvironmentVariable(tool.ToUpperInvariant());
            if (!string.IsNullOrWhiteSpace(overrideVariable) && File.Exists(overrideVariable)) return overrideVariable;
            List<string> candidates = new List<string>();
            string[] extensions = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", ".bat" } : new[] { "" };
            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                foreach (string extension in extensions) candidates.Add(Path.Combine(directory.Trim('"'), tool + extension));
            if (OperatingSystem.IsWindows() && tool == "iscc")
            {
                foreach (string? programFiles in new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramFiles") })
                    if (!string.IsNullOrEmpty(programFiles)) candidates.Add(Path.Combine(programFiles, "Inno Setup 6", "ISCC.exe"));
            }
            if (OperatingSystem.IsWindows() && tool == "signtool")
            {
                string kits = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)", "Windows Kits", "10", "bin");
                if (Directory.Exists(kits))
                    candidates.AddRange(Directory.GetDirectories(kits).OrderByDescending(path => path, StringComparer.Ordinal).Select(path => Path.Combine(path, "x64", "signtool.exe")));
            }
            return candidates.FirstOrDefault(File.Exists);
        }

        /// <summary>Finds a required tool or fails with the command that installs it.</summary>
        /// <param name="tool">Tool name.</param>
        /// <returns>The absolute path.</returns>
        /// <exception cref="PublisherException">The tool is missing; the message holds the install command.</exception>
        public static string Require(string tool) => Find(tool) ?? throw new PublisherException("Required tool '" + tool + "' was not found. " + InstallHint(tool));

        /// <summary>Runs a tool, streaming its output, and fails when it exits nonzero.</summary>
        /// <param name="executable">Absolute path or name of the executable.</param>
        /// <param name="arguments">Arguments, passed without shell interpretation.</param>
        /// <param name="workingDirectory">Working directory, or null for the current directory.</param>
        /// <param name="environment">Extra environment variables, or null.</param>
        /// <param name="cancellationToken">Cancels the run and kills the tool.</param>
        /// <returns>A task that completes when the tool exits successfully.</returns>
        /// <exception cref="PublisherException">The tool exited nonzero.</exception>
        /// <exception cref="OperationCanceledException">The run was canceled.</exception>
        public static async Task RunAsync(string executable, IEnumerable<string> arguments, string? workingDirectory, IReadOnlyDictionary<string, string>? environment, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executable);
            ArgumentNullException.ThrowIfNull(arguments);
            ProcessStartInfo info = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory };
            foreach (string argument in arguments) info.ArgumentList.Add(argument);
            if (environment != null)
                foreach (KeyValuePair<string, string> pair in environment) info.Environment[pair.Key] = pair.Value;
            Console.WriteLine("> " + Path.GetFileName(executable) + " " + string.Join(" ", info.ArgumentList.Select(Display)));
            using Process process = Process.Start(info) ?? throw new PublisherException("Could not start " + executable + ".");
            try { await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }
            if (process.ExitCode != 0) throw new PublisherException(Path.GetFileName(executable) + " exited with code " + process.ExitCode + ".");
        }

        /// <summary>Runs a required tool by name.</summary>
        /// <param name="tool">Tool name resolved with <see cref="Require"/>.</param>
        /// <param name="arguments">Arguments.</param>
        /// <param name="cancellationToken">Cancels the run.</param>
        /// <returns>A task that completes when the tool exits successfully.</returns>
        /// <exception cref="PublisherException">The tool is missing or exited nonzero.</exception>
        public static Task RunToolAsync(string tool, IEnumerable<string> arguments, CancellationToken cancellationToken) => RunAsync(Require(tool), arguments, null, null, cancellationToken);

        private static string Display(string argument) => argument.Contains(' ', StringComparison.Ordinal) ? "\"" + argument + "\"" : argument;
    }
}
