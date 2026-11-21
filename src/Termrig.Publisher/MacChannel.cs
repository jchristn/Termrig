namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Assembles and signs the .app bundle, then wraps it in a .pkg and a .dmg.</summary>
    public static class MacChannel
    {
        /// <summary>Returns the staged bundle path for a target.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="target">A macOS runtime identifier.</param>
        /// <returns>The bundle directory.</returns>
        public static string BundlePath(PublisherContext context, RuntimeTarget target)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(target);
            return Path.Combine(context.WorkDirectory, "macos", target.Rid, "app", MacPackageWriter.BundleName(context));
        }

        /// <summary>Assembles, signs, and verifies the .app bundle for every macOS target.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="cancellationToken">Cancels the build.</param>
        /// <returns>A task that completes when every bundle is signed.</returns>
        public static async Task AppAsync(PublisherContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            foreach (RuntimeTarget target in context.TargetsFor("macos")) await BuildBundleAsync(context, target, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Builds the installer package for every macOS target.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="cancellationToken">Cancels the build.</param>
        /// <returns>A task that completes when every package is written.</returns>
        public static async Task PkgAsync(PublisherContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ToolRunner.Require("pkgbuild");
            ToolRunner.Require("productbuild");
            Directory.CreateDirectory(context.OutputDirectory);
            ProjectInfo project = context.Manifest.Project;
            foreach (RuntimeTarget target in context.TargetsFor("macos"))
            {
                string bundle = await EnsureBundleAsync(context, target, cancellationToken).ConfigureAwait(false);
                string work = FileStaging.Fresh(Path.Combine(context.WorkDirectory, "macos", target.Rid, "pkg"));
                string root = Path.Combine(work, "root");
                Directory.CreateDirectory(Path.Combine(root, "Applications"));
                await ToolRunner.RunToolAsync("ditto", new[] { bundle, Path.Combine(root, "Applications", MacPackageWriter.BundleName(context)) }, cancellationToken).ConfigureAwait(false);
                string scripts = Path.Combine(work, "scripts");
                FileStaging.WriteText(Path.Combine(scripts, "postinstall"), MacPackageWriter.Postinstall(context), executable: true);
                string componentPlist = Path.Combine(work, "component.plist");
                FileStaging.WriteText(componentPlist, MacPackageWriter.ComponentPlist(context));
                string componentName = project.Command + "-component.pkg";
                string component = Path.Combine(work, componentName);
                await ToolRunner.RunToolAsync("pkgbuild", new[]
                {
                    "--root", root, "--component-plist", componentPlist, "--identifier", project.Identifier, "--version", context.Version,
                    "--install-location", "/", "--scripts", scripts, component
                }, cancellationToken).ConfigureAwait(false);
                string resources = Path.Combine(work, "resources");
                FileStaging.Copy(Path.Combine(context.ArtworkDirectory, "pkg-background.png"), Path.Combine(resources, "background.png"), executable: false);
                FileStaging.Copy(Path.Combine(context.ArtworkDirectory, "pkg-background-dark.png"), Path.Combine(resources, "background-dark.png"), executable: false);
                FileStaging.WriteText(Path.Combine(resources, "welcome.html"), MacPackageWriter.Welcome(context));
                FileStaging.WriteText(Path.Combine(resources, "LICENSE.txt"), context.LicenseText());
                string distribution = Path.Combine(work, "distribution.xml");
                FileStaging.WriteText(distribution, MacPackageWriter.Distribution(context, target, componentName));
                string output = context.OutputFile("pkg", target);
                List<string> arguments = new List<string> { "--distribution", distribution, "--resources", resources, "--package-path", work };
                SigningTarget signing = context.Manifest.Signing.Macos;
                if (signing.IsConfigured && !string.IsNullOrWhiteSpace(signing.InstallerIdentity)) arguments.AddRange(new[] { "--sign", signing.InstallerIdentity });
                arguments.Add(output);
                await ToolRunner.RunToolAsync("productbuild", arguments, cancellationToken).ConfigureAwait(false);
                await NotarizeAsync(context, output, cancellationToken).ConfigureAwait(false);
                Console.WriteLine("Wrote " + output);
            }
        }

        /// <summary>Builds the drag-to-Applications disk image for every macOS target.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="cancellationToken">Cancels the build.</param>
        /// <returns>A task that completes when every image is written.</returns>
        public static async Task DmgAsync(PublisherContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ToolRunner.Require("hdiutil");
            Directory.CreateDirectory(context.OutputDirectory);
            foreach (RuntimeTarget target in context.TargetsFor("macos"))
            {
                string bundle = await EnsureBundleAsync(context, target, cancellationToken).ConfigureAwait(false);
                string staging = FileStaging.Fresh(Path.Combine(context.WorkDirectory, "macos", target.Rid, "dmg"));
                await ToolRunner.RunToolAsync("ditto", new[] { bundle, Path.Combine(staging, MacPackageWriter.BundleName(context)) }, cancellationToken).ConfigureAwait(false);
                File.CreateSymbolicLink(Path.Combine(staging, "Applications"), "/Applications");
                FileStaging.WriteText(Path.Combine(staging, MacPackageWriter.DmgNoteName(context)), MacPackageWriter.DmgNote(context));
                long megabytes = FileStaging.Size(staging) * 125 / 100 / (1024 * 1024) + 64;
                string output = context.OutputFile("dmg", target);
                if (File.Exists(output)) File.Delete(output);
                await ToolRunner.RunToolAsync("hdiutil", new[]
                {
                    "create", "-volname", context.Manifest.Project.Name + " " + context.Version, "-srcfolder", staging,
                    "-fs", "HFS+", "-format", "UDZO", "-size", megabytes + "m", "-ov", output
                }, cancellationToken).ConfigureAwait(false);
                await NotarizeAsync(context, output, cancellationToken).ConfigureAwait(false);
                Console.WriteLine("Wrote " + output);
            }
        }

        /// <summary>Signs code ad-hoc, or with the Developer ID identity and hardened runtime when signing is configured.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="path">A bundle or executable.</param>
        /// <param name="deep">True to sign nested code in a bundle.</param>
        /// <param name="cancellationToken">Cancels signing.</param>
        /// <returns>A task that completes when the code is signed.</returns>
        public static async Task CodesignAsync(PublisherContext context, string path, bool deep, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            SigningTarget signing = context.Manifest.Signing.Macos;
            List<string> arguments = new List<string> { "--force" };
            if (deep) arguments.Add("--deep");
            if (signing.IsConfigured)
            {
                string entitlements = Path.Combine(context.WorkDirectory, "macos", "entitlements.plist");
                FileStaging.WriteText(entitlements, MacPackageWriter.Entitlements());
                arguments.AddRange(new[] { "--options", "runtime", "--timestamp", "--entitlements", entitlements, "--sign", signing.Identity });
            }
            else
            {
                arguments.AddRange(new[] { "--sign", "-" });
            }
            arguments.Add(path);
            await ToolRunner.RunToolAsync("codesign", arguments, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<string> EnsureBundleAsync(PublisherContext context, RuntimeTarget target, CancellationToken cancellationToken)
        {
            string bundle = BundlePath(context, target);
            if (!Directory.Exists(bundle)) await BuildBundleAsync(context, target, cancellationToken).ConfigureAwait(false);
            return bundle;
        }

        private static async Task BuildBundleAsync(PublisherContext context, RuntimeTarget target, CancellationToken cancellationToken)
        {
            ToolRunner.Require("codesign");
            if (!File.Exists(Path.Combine(context.ArtworkDirectory, ArtworkBuilder.IcnsFile))) ArtworkBuilder.Build(context);
            string bundle = BundlePath(context, target);
            FileStaging.Fresh(bundle);
            string contents = Path.Combine(bundle, "Contents");
            foreach (ArtifactInfo artifact in context.Manifest.Artifacts)
                FileStaging.Copy(Path.Combine(context.PayloadDirectory(target), artifact.Executable), Path.Combine(contents, "MacOS", artifact.Executable), executable: true);
            foreach (string extra in context.PayloadExtras(target))
                FileStaging.Copy(Path.Combine(context.PayloadDirectory(target), extra), Path.Combine(contents, "Resources", extra), executable: false);
            FileStaging.WriteText(Path.Combine(contents, "Info.plist"), MacPackageWriter.InfoPlist(context));
            FileStaging.Copy(Path.Combine(context.ArtworkDirectory, ArtworkBuilder.IcnsFile), Path.Combine(contents, "Resources", ArtworkBuilder.IcnsFile), executable: false);
            FileStaging.WriteText(Path.Combine(contents, "Resources", "LICENSE.txt"), context.LicenseText());
            FileStaging.WriteText(Path.Combine(contents, "Resources", "uninstall.sh"), MacPackageWriter.Uninstall(context), executable: true);
            // Sign last, after every file is in place: a later change invalidates the signature and Gatekeeper reports the app as damaged.
            foreach (ArtifactInfo artifact in context.Manifest.Artifacts)
                if (artifact.Executable != context.Main.Executable)
                    await CodesignAsync(context, Path.Combine(contents, "MacOS", artifact.Executable), deep: false, cancellationToken).ConfigureAwait(false);
            await CodesignAsync(context, bundle, deep: true, cancellationToken).ConfigureAwait(false);
            await ToolRunner.RunToolAsync("codesign", new[] { "--verify", "--deep", "--strict", "--verbose=2", bundle }, cancellationToken).ConfigureAwait(false);
            Console.WriteLine("Signed " + bundle);
        }

        private static async Task NotarizeAsync(PublisherContext context, string file, CancellationToken cancellationToken)
        {
            SigningTarget signing = context.Manifest.Signing.Macos;
            if (!signing.IsConfigured || string.IsNullOrWhiteSpace(signing.NotaryProfile)) return;
            await ToolRunner.RunToolAsync("xcrun", new[] { "notarytool", "submit", file, "--keychain-profile", signing.NotaryProfile, "--wait" }, cancellationToken).ConfigureAwait(false);
            await ToolRunner.RunToolAsync("xcrun", new[] { "stapler", "staple", file }, cancellationToken).ConfigureAwait(false);
        }
    }
}
