namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Builds .deb and .rpm packages with fpm and AppImages with appimagetool.</summary>
    public static class LinuxChannel
    {
        /// <summary>Builds one .deb per Linux target.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="cancellationToken">Cancels the build.</param>
        /// <returns>A task that completes when every package is written.</returns>
        public static Task DebAsync(PublisherContext context, CancellationToken cancellationToken) => FpmAsync(context, "deb", cancellationToken);

        /// <summary>Builds one .rpm per Linux target.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="cancellationToken">Cancels the build.</param>
        /// <returns>A task that completes when every package is written.</returns>
        public static Task RpmAsync(PublisherContext context, CancellationToken cancellationToken)
        {
            ToolRunner.Require("rpmbuild");
            return FpmAsync(context, "rpm", cancellationToken);
        }

        /// <summary>Lays out the package file system: executables, PATH symlink, menu and autostart entries, icons, and license.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="target">A Linux runtime identifier.</param>
        /// <returns>The staging root.</returns>
        public static string Stage(PublisherContext context, RuntimeTarget target)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(target);
            ProjectInfo project = context.Manifest.Project;
            string root = FileStaging.Fresh(Path.Combine(context.WorkDirectory, "linux", target.Rid, "root"));
            string install = context.Manifest.Linux.InstallDirectory.TrimStart('/');
            foreach (ArtifactInfo artifact in context.Manifest.Artifacts)
                FileStaging.Copy(Path.Combine(context.PayloadDirectory(target), artifact.Executable), Path.Combine(root, install, artifact.Executable), executable: true);
            foreach (string extra in context.PayloadExtras(target))
                FileStaging.Copy(Path.Combine(context.PayloadDirectory(target), extra), Path.Combine(root, install, extra), executable: false);
            Directory.CreateDirectory(Path.Combine(root, "usr", "bin"));
            File.CreateSymbolicLink(Path.Combine(root, "usr", "bin", context.PathCommand), "/" + install + "/" + context.Cli.Executable);
            if (context.HasMenuEntry)
            {
                string executable = "/" + install + "/" + context.Main.Executable;
                FileStaging.WriteText(Path.Combine(root, "usr", "share", "applications", project.Identifier + ".desktop"), LinuxPackageWriter.DesktopEntry(context, executable, autostart: false));
            }
            if (context.Tray != null)
            {
                string executable = "/" + install + "/" + context.Tray.Executable;
                FileStaging.WriteText(Path.Combine(root, "etc", "xdg", "autostart", project.Identifier + ".desktop"), LinuxPackageWriter.DesktopEntry(context, executable, autostart: true));
            }
            foreach (int size in ArtworkBuilder.LinuxIconSizes)
                FileStaging.Copy(Path.Combine(context.ArtworkDirectory, "icon-" + size + ".png"), Path.Combine(root, "usr", "share", "icons", "hicolor", size + "x" + size, "apps", project.Identifier + ".png"), executable: false);
            FileStaging.WriteText(Path.Combine(root, "usr", "share", "doc", project.Command, "copyright"), context.LicenseText());
            FileStaging.WriteText(Path.Combine(root, "usr", "share", "licenses", project.Command, "LICENSE"), context.LicenseText());
            SetDirectoryModes(root);
            return root;
        }

        /// <summary>Builds one AppImage per Linux target.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="cancellationToken">Cancels the build.</param>
        /// <returns>A task that completes when every AppImage is written.</returns>
        public static async Task AppImageAsync(PublisherContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            string tool = ToolRunner.Require("appimagetool");
            EnsureArtwork(context);
            Directory.CreateDirectory(context.OutputDirectory);
            ProjectInfo project = context.Manifest.Project;
            foreach (RuntimeTarget target in context.TargetsFor("linux"))
            {
                string appDir = FileStaging.Fresh(Path.Combine(context.WorkDirectory, "linux", target.Rid, project.Name + ".AppDir"));
                foreach (ArtifactInfo artifact in context.Manifest.Artifacts)
                    FileStaging.Copy(Path.Combine(context.PayloadDirectory(target), artifact.Executable), Path.Combine(appDir, "usr", "bin", artifact.Executable), executable: true);
                foreach (string extra in context.PayloadExtras(target))
                    FileStaging.Copy(Path.Combine(context.PayloadDirectory(target), extra), Path.Combine(appDir, "usr", "bin", extra), executable: false);
                FileStaging.WriteText(Path.Combine(appDir, "AppRun"), LinuxPackageWriter.AppRun(context), executable: true);
                // AppRun sets the launch environment, so the AppImage's own entry names the bare executable.
                FileStaging.WriteText(Path.Combine(appDir, project.Identifier + ".desktop"), LinuxPackageWriter.DesktopEntry(context, context.Main.Executable, autostart: false, launchEnvironment: false));
                string icon = Path.Combine(context.ArtworkDirectory, "icon-256.png");
                FileStaging.Copy(icon, Path.Combine(appDir, project.Identifier + ".png"), executable: false);
                FileStaging.Copy(icon, Path.Combine(appDir, ".DirIcon"), executable: false);
                FileStaging.Copy(icon, Path.Combine(appDir, "usr", "share", "icons", "hicolor", "256x256", "apps", project.Identifier + ".png"), executable: false);
                FileStaging.WriteText(Path.Combine(appDir, "usr", "share", "licenses", project.Command, "LICENSE"), context.LicenseText());
                SetDirectoryModes(appDir);
                string output = context.OutputFile("appimage", target);
                Dictionary<string, string> environment = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ARCH"] = target.GnuArchitecture,
                    ["APPIMAGE_EXTRACT_AND_RUN"] = "1"
                };
                await ToolRunner.RunAsync(tool, new[] { "--no-appstream", appDir, output }, context.WorkDirectory, environment, cancellationToken).ConfigureAwait(false);
                if (!File.Exists(output)) throw new PublisherException("appimagetool did not produce " + output + ".");
                Console.WriteLine("Wrote " + output);
            }
        }

        private static async Task FpmAsync(PublisherContext context, string format, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            string fpm = ToolRunner.Require("fpm");
            EnsureArtwork(context);
            Directory.CreateDirectory(context.OutputDirectory);
            foreach (RuntimeTarget target in context.TargetsFor("linux"))
            {
                string root = Stage(context, target);
                string output = context.OutputFile(format, target);
                await ToolRunner.RunAsync(fpm, LinuxPackageWriter.FpmArguments(context, target, format, root, output), context.WorkDirectory, null, cancellationToken).ConfigureAwait(false);
                if (!File.Exists(output)) throw new PublisherException("fpm did not produce " + output + ".");
                Console.WriteLine("Wrote " + output);
            }
        }

        private static void EnsureArtwork(PublisherContext context)
        {
            if (!File.Exists(Path.Combine(context.ArtworkDirectory, "icon-256.png"))) ArtworkBuilder.Build(context);
        }

        private static void SetDirectoryModes(string root)
        {
            if (OperatingSystem.IsWindows()) return;
            UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(root, mode);
            foreach (string directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
                if (new DirectoryInfo(directory).LinkTarget == null) File.SetUnixFileMode(directory, mode);
        }
    }
}
