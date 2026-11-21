namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>Writes Linux desktop entries, the AppImage entry point, and fpm arguments.</summary>
    public static class LinuxPackageWriter
    {
        /// <summary>Builds a freedesktop entry for the application menu, autostart, or an AppImage.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="executable">Value for <c>Exec=</c>, without arguments.</param>
        /// <param name="autostart">True to append the agent arguments and autostart keys.</param>
        /// <param name="launchEnvironment">True to start the executable through <c>env</c> with the manifest's launch environment.</param>
        /// <returns>The .desktop file text.</returns>
        public static string DesktopEntry(PublisherContext context, string executable, bool autostart, bool launchEnvironment = true)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(executable);
            ProjectInfo project = context.Manifest.Project;
            StringBuilder entry = new StringBuilder();
            entry.Append("[Desktop Entry]\n");
            entry.Append("Type=Application\n");
            entry.Append("Name=").Append(project.Name).Append('\n');
            entry.Append("Comment=").Append(project.Tagline).Append('\n');
            string arguments = autostart ? string.Concat(context.Manifest.Agent.Arguments.Select(argument => " " + Quote(argument))) : "";
            string environment = launchEnvironment && context.Manifest.LaunchEnvironment.Count > 0
                ? "env " + string.Concat(context.Manifest.LaunchEnvironment.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value + " "))
                : "";
            entry.Append("Exec=").Append(environment).Append(Quote(executable)).Append(arguments).Append('\n');
            entry.Append("Icon=").Append(project.Identifier).Append('\n');
            entry.Append("Terminal=false\n");
            entry.Append("Categories=").Append(context.Manifest.Linux.DesktopCategories).Append('\n');
            entry.Append("StartupWMClass=").Append(project.Name).Append('\n');
            if (autostart) entry.Append("X-GNOME-Autostart-enabled=true\n");
            return entry.ToString();
        }

        /// <summary>
        /// Builds the AppImage entry point: exports the launch environment and runs the main executable, or the CLI when
        /// the first argument is <c>--cli</c> and the CLI is a separate executable.
        /// </summary>
        /// <param name="context">The publisher context.</param>
        /// <returns>The AppRun script, with LF line endings.</returns>
        public static string AppRun(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            string main = context.Main.Executable;
            StringBuilder script = new StringBuilder();
            script.Append("#!/bin/sh\n");
            script.Append("# Runs ").Append(context.Manifest.Project.Name).Append(" from inside the AppImage.");
            if (main != context.Cli.Executable) script.Append(" Pass --cli to run the ").Append(context.PathCommand).Append(" command instead.");
            script.Append('\n');
            script.Append("here=\"$(dirname \"$(readlink -f \"$0\")\")\"\n");
            foreach (KeyValuePair<string, string> variable in context.Manifest.LaunchEnvironment.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                script.Append("export ").Append(variable.Key).Append('=').Append(variable.Value).Append('\n');
            if (main != context.Cli.Executable)
            {
                script.Append("if [ \"${1:-}\" = \"--cli\" ]; then\n");
                script.Append("  shift\n");
                script.Append("  exec \"$here/usr/bin/").Append(context.Cli.Executable).Append("\" \"$@\"\n");
                script.Append("fi\n");
            }
            script.Append("exec \"$here/usr/bin/").Append(main).Append("\" \"$@\"\n");
            return script.ToString();
        }

        /// <summary>Builds the fpm arguments for a .deb or .rpm.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="target">A Linux runtime identifier.</param>
        /// <param name="format"><c>deb</c> or <c>rpm</c>.</param>
        /// <param name="stagingRoot">Directory laid out like the target file system.</param>
        /// <param name="output">Output package path.</param>
        /// <returns>The argument list, without the fpm executable.</returns>
        /// <exception cref="PublisherException">The format is not deb or rpm.</exception>
        public static IReadOnlyList<string> FpmArguments(PublisherContext context, RuntimeTarget target, string format, string stagingRoot, string output)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(target);
            if (format != "deb" && format != "rpm") throw new PublisherException("fpm format must be deb or rpm, not '" + format + "'.");
            ProjectInfo project = context.Manifest.Project;
            List<string> arguments = new List<string>
            {
                "-s", "dir", "-t", format,
                "-n", project.Command,
                "-v", format == "deb" ? AssetNames.DebianVersion(context.Version) : AssetNames.RpmVersion(context.Version),
                "-a", format == "deb" ? target.DebianArchitecture : target.GnuArchitecture,
                "--description", project.Tagline + "\n" + project.Description,
                "--license", project.License,
                "--url", project.RepositoryUrl,
                "--vendor", project.Publisher,
                "--maintainer", project.Publisher,
                "--category", context.Manifest.Linux.Category,
                "--force",
                "-C", stagingRoot,
                "-p", output
            };
            if (format == "rpm") arguments.AddRange(new[] { "--iteration", "1", "--rpm-os", "linux" });
            if (format == "deb") arguments.AddRange(new[] { "--deb-no-default-config-files" });
            foreach (string dependency in format == "deb" ? context.Manifest.Linux.DebDependencies : context.Manifest.Linux.RpmDependencies)
            {
                arguments.Add("-d");
                arguments.Add(dependency);
            }
            // Name only the top-level directories that were staged: fpm fails on a missing path, and /etc exists only
            // when a tray artifact installs an autostart entry.
            IEnumerable<string> roots = new[] { "opt", "usr", "etc" };
            if (Directory.Exists(stagingRoot)) roots = roots.Where(root => Directory.Exists(Path.Combine(stagingRoot, root)));
            arguments.AddRange(roots);
            return arguments;
        }

        private static string Quote(string value) =>
            value.Any(character => char.IsWhiteSpace(character) || "\"`$\\".Contains(character, StringComparison.Ordinal))
                ? "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal).Replace("`", "\\`", StringComparison.Ordinal).Replace("$", "\\$", StringComparison.Ordinal) + "\""
                : value;
    }
}
