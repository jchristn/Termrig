namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security;
    using System.Text;

    /// <summary>Writes the macOS bundle metadata, installer distribution, and install scripts.</summary>
    public static class MacPackageWriter
    {
        /// <summary>Returns the application bundle name, for example <c>Termrig.app</c>.</summary>
        /// <param name="context">The publisher context.</param>
        /// <returns>The bundle directory name.</returns>
        public static string BundleName(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return context.Manifest.Project.Name + ".app";
        }

        /// <summary>Returns the installed bundle path, for example <c>/Applications/Termrig.app</c>.</summary>
        /// <param name="context">The publisher context.</param>
        /// <returns>The absolute install path.</returns>
        public static string InstalledBundle(PublisherContext context) => "/Applications/" + BundleName(context);

        /// <summary>Builds the bundle's Info.plist.</summary>
        /// <param name="context">The publisher context.</param>
        /// <returns>The property list XML.</returns>
        public static string InfoPlist(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ProjectInfo project = context.Manifest.Project;
            string main = context.Main.Executable;
            StringBuilder plist = new StringBuilder();
            plist.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            plist.Append("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n");
            plist.Append("<plist version=\"1.0\">\n<dict>\n");
            Entry(plist, "CFBundleName", project.Name);
            Entry(plist, "CFBundleDisplayName", project.Name);
            Entry(plist, "CFBundleIdentifier", project.Identifier);
            Entry(plist, "CFBundleExecutable", main);
            Entry(plist, "CFBundlePackageType", "APPL");
            Entry(plist, "CFBundleShortVersionString", context.Version);
            Entry(plist, "CFBundleVersion", context.Version);
            Entry(plist, "CFBundleIconFile", ArtworkBuilder.IcnsFile);
            Entry(plist, "CFBundleInfoDictionaryVersion", "6.0");
            Entry(plist, "LSMinimumSystemVersion", context.Manifest.Macos.MinimumSystemVersion);
            Entry(plist, "LSApplicationCategoryType", "public.app-category.developer-tools");
            if (!string.IsNullOrWhiteSpace(project.Copyright)) Entry(plist, "NSHumanReadableCopyright", project.Copyright);
            plist.Append("  <key>NSHighResolutionCapable</key><true/>\n");
            if (context.Manifest.LaunchEnvironment.Count > 0)
            {
                plist.Append("  <key>LSEnvironment</key>\n  <dict>\n");
                foreach (KeyValuePair<string, string> variable in context.Manifest.LaunchEnvironment.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    plist.Append("    <key>").Append(SecurityElement.Escape(variable.Key)).Append("</key><string>").Append(SecurityElement.Escape(variable.Value)).Append("</string>\n");
                plist.Append("  </dict>\n");
            }
            plist.Append("</dict>\n</plist>\n");
            return plist.ToString();
        }

        /// <summary>Builds the per-user LaunchAgent that starts the tray executable at login.</summary>
        /// <param name="context">The publisher context; must have a tray artifact.</param>
        /// <returns>The property list XML.</returns>
        /// <exception cref="PublisherException">The manifest has no tray artifact.</exception>
        public static string LaunchAgentPlist(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArtifactInfo tray = context.Tray ?? throw new PublisherException("A LaunchAgent needs a tray artifact.");
            StringBuilder plist = new StringBuilder();
            plist.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            plist.Append("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n");
            plist.Append("<plist version=\"1.0\">\n<dict>\n");
            Entry(plist, "Label", context.Manifest.Agent.Label);
            plist.Append("  <key>ProgramArguments</key>\n  <array>\n");
            plist.Append("    <string>").Append(SecurityElement.Escape(InstalledBundle(context) + "/Contents/MacOS/" + tray.Executable)).Append("</string>\n");
            foreach (string argument in context.Manifest.Agent.Arguments) plist.Append("    <string>").Append(SecurityElement.Escape(argument)).Append("</string>\n");
            plist.Append("  </array>\n");
            plist.Append("  <key>RunAtLoad</key><true/>\n");
            plist.Append("  <key>LimitLoadToSessionType</key><string>Aqua</string>\n");
            plist.Append("  <key>ProcessType</key><string>Interactive</string>\n");
            plist.Append("</dict>\n</plist>\n");
            return plist.ToString();
        }

        /// <summary>Builds the productbuild distribution file with title, logo, welcome page, and license.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="target">A macOS runtime identifier.</param>
        /// <param name="componentPackage">File name of the component package.</param>
        /// <returns>The distribution XML.</returns>
        public static string Distribution(PublisherContext context, RuntimeTarget target, string componentPackage)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(target);
            ProjectInfo project = context.Manifest.Project;
            string identifier = SecurityElement.Escape(project.Identifier) ?? "";
            StringBuilder xml = new StringBuilder();
            xml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            xml.Append("<installer-gui-script minSpecVersion=\"2\">\n");
            xml.Append("  <title>").Append(SecurityElement.Escape(project.Name)).Append("</title>\n");
            xml.Append("  <organization>").Append(identifier).Append("</organization>\n");
            xml.Append("  <background file=\"background.png\" mime-type=\"image/png\" alignment=\"bottomleft\" scaling=\"none\"/>\n");
            xml.Append("  <background-darkAqua file=\"background-dark.png\" mime-type=\"image/png\" alignment=\"bottomleft\" scaling=\"none\"/>\n");
            xml.Append("  <welcome file=\"welcome.html\" mime-type=\"text/html\"/>\n");
            xml.Append("  <license file=\"LICENSE.txt\" mime-type=\"text/plain\"/>\n");
            xml.Append("  <options customize=\"never\" require-scripts=\"false\" hostArchitectures=\"").Append(target.MacArchitecture).Append("\"/>\n");
            xml.Append("  <domains enable_anywhere=\"false\" enable_currentUserHome=\"false\" enable_localSystem=\"true\"/>\n");
            xml.Append("  <volume-check><allowed-os-versions><os-version min=\"").Append(SecurityElement.Escape(context.Manifest.Macos.MinimumSystemVersion)).Append("\"/></allowed-os-versions></volume-check>\n");
            xml.Append("  <choices-outline><line choice=\"default\"><line choice=\"").Append(identifier).Append("\"/></line></choices-outline>\n");
            xml.Append("  <choice id=\"default\"/>\n");
            xml.Append("  <choice id=\"").Append(identifier).Append("\" visible=\"false\"><pkg-ref id=\"").Append(identifier).Append("\"/></choice>\n");
            xml.Append("  <pkg-ref id=\"").Append(identifier).Append("\" version=\"").Append(SecurityElement.Escape(context.Version)).Append("\" onConclusion=\"none\">").Append(SecurityElement.Escape(componentPackage)).Append("</pkg-ref>\n");
            xml.Append("</installer-gui-script>\n");
            return xml.ToString();
        }

        /// <summary>Builds the installer's welcome page: the tagline and what will be installed.</summary>
        /// <param name="context">The publisher context.</param>
        /// <returns>The HTML page.</returns>
        public static string Welcome(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ProjectInfo project = context.Manifest.Project;
            StringBuilder html = new StringBuilder();
            html.Append("<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"><style>body{font-family:-apple-system,sans-serif;font-size:13px;line-height:1.45;margin-left:12px}h1{font-size:18px}</style></head><body>\n");
            html.Append("<h1>").Append(Html(project.Name + " " + context.Version)).Append("</h1>\n");
            html.Append("<p><strong>").Append(Html(project.Tagline)).Append("</strong></p>\n");
            html.Append("<p>").Append(Html(project.Description)).Append("</p>\n");
            html.Append("<p>This installer will:</p>\n<ul>\n");
            html.Append("<li>Install <code>").Append(Html(BundleName(context))).Append("</code> in your Applications folder.</li>\n");
            html.Append("<li>Add the <code>").Append(Html(context.PathCommand)).Append("</code> command at <code>").Append(Html(context.Manifest.Macos.CommandDirectory + "/" + context.PathCommand)).Append("</code>.</li>\n");
            if (context.Tray != null) html.Append("<li>Start ").Append(Html(project.Name)).Append(" in the menu bar when you log in. Quit it from the menu bar icon at any time.</li>\n");
            html.Append("</ul>\n");
            html.Append("<p>Published by ").Append(Html(project.Publisher)).Append(". Source code: <a href=\"").Append(Html(project.RepositoryUrl)).Append("\">").Append(Html(project.RepositoryUrl)).Append("</a></p>\n");
            html.Append("</body></html>\n");
            return html.ToString();
        }

        /// <summary>Builds the postinstall script: PATH symlink, LaunchAgent, and starting the agent for the console user.</summary>
        /// <param name="context">The publisher context.</param>
        /// <returns>The script, with LF line endings.</returns>
        public static string Postinstall(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            string bundle = InstalledBundle(context);
            string commandDirectory = context.Manifest.Macos.CommandDirectory;
            StringBuilder script = new StringBuilder();
            script.Append("#!/bin/sh\n");
            script.Append("# Generated by ").Append(context.Manifest.Project.Name).Append(".Publisher. Runs as root after the payload is installed.\n");
            script.Append("set -u\n");
            script.Append("mkdir -p ").Append(Shell(commandDirectory)).Append('\n');
            script.Append("ln -sf ").Append(Shell(bundle + "/Contents/MacOS/" + context.Cli.Executable)).Append(' ').Append(Shell(commandDirectory + "/" + context.PathCommand)).Append('\n');
            if (context.Tray != null)
            {
                string label = context.Manifest.Agent.Label;
                string plist = "/Library/LaunchAgents/" + label + ".plist";
                script.Append("plist=").Append(Shell(plist)).Append('\n');
                script.Append("mkdir -p /Library/LaunchAgents\n");
                script.Append("cat > \"$plist\" <<'PLIST'\n").Append(LaunchAgentPlist(context)).Append("PLIST\n");
                script.Append("chown root:wheel \"$plist\"\n");
                script.Append("chmod 644 \"$plist\"\n");
                script.Append("user=$(stat -f%Su /dev/console 2>/dev/null || true)\n");
                script.Append("if [ -n \"$user\" ] && [ \"$user\" != root ] && [ \"$user\" != loginwindow ]; then\n");
                script.Append("  uid=$(id -u \"$user\")\n");
                script.Append("  launchctl bootout \"gui/$uid/").Append(label).Append("\" 2>/dev/null || true\n");
                script.Append("  launchctl bootstrap \"gui/$uid\" \"$plist\" 2>/dev/null || true\n");
                script.Append("fi\n");
            }
            script.Append("exit 0\n");
            return script.ToString();
        }

        /// <summary>Builds the uninstall script shipped inside the bundle's Resources directory.</summary>
        /// <param name="context">The publisher context.</param>
        /// <returns>The script, with LF line endings.</returns>
        public static string Uninstall(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            ProjectInfo project = context.Manifest.Project;
            string bundle = InstalledBundle(context);
            string link = context.Manifest.Macos.CommandDirectory + "/" + context.PathCommand;
            string kept = string.IsNullOrWhiteSpace(project.DataDirectory) ? "" : " Your data in " + project.DataDirectory + " is kept.";
            StringBuilder script = new StringBuilder();
            script.Append("#!/bin/sh\n");
            script.Append("# Removes ").Append(project.Name).Append(" installed from the .pkg.").Append(kept).Append(" Run with sudo.\n");
            script.Append("set -u\n");
            script.Append("if [ \"$(id -u)\" -ne 0 ]; then echo 'Run with sudo: sudo ").Append(bundle).Append("/Contents/Resources/uninstall.sh' >&2; exit 1; fi\n");
            if (context.Tray != null)
            {
                string label = context.Manifest.Agent.Label;
                script.Append("user=$(stat -f%Su /dev/console 2>/dev/null || true)\n");
                script.Append("if [ -n \"$user\" ] && [ \"$user\" != root ] && [ \"$user\" != loginwindow ]; then\n");
                script.Append("  launchctl bootout \"gui/$(id -u \"$user\")/").Append(label).Append("\" 2>/dev/null || true\n");
                script.Append("fi\n");
                script.Append("rm -f ").Append(Shell("/Library/LaunchAgents/" + label + ".plist")).Append('\n');
            }
            script.Append("if [ -L ").Append(Shell(link)).Append(" ]; then rm -f ").Append(Shell(link)).Append("; fi\n");
            script.Append("rm -rf ").Append(Shell(bundle)).Append('\n');
            script.Append("pkgutil --forget ").Append(Shell(project.Identifier)).Append(" >/dev/null 2>&1 || true\n");
            script.Append("echo ").Append(Shell(project.Name + " removed." + kept)).Append('\n');
            return script.ToString();
        }

        /// <summary>Builds the pkgbuild component property list that pins the bundle to /Applications.</summary>
        /// <param name="context">The publisher context.</param>
        /// <returns>The property list XML.</returns>
        public static string ComponentPlist(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            StringBuilder plist = new StringBuilder();
            plist.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            plist.Append("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n");
            plist.Append("<plist version=\"1.0\">\n<array>\n<dict>\n");
            plist.Append("  <key>BundleHasStrictIdentifier</key><true/>\n");
            plist.Append("  <key>BundleIsRelocatable</key><false/>\n");
            plist.Append("  <key>BundleIsVersionChecked</key><false/>\n");
            plist.Append("  <key>BundleOverwriteAction</key><string>upgrade</string>\n");
            Entry(plist, "RootRelativeBundlePath", "Applications/" + BundleName(context));
            plist.Append("</dict>\n</array>\n</plist>\n");
            return plist.ToString();
        }

        /// <summary>Returns the name of the note placed beside the app in the disk image.</summary>
        /// <param name="context">The publisher context.</param>
        /// <returns>For example <c>If Termrig won't open.txt</c>.</returns>
        public static string DmgNoteName(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            return "If " + context.Manifest.Project.Name + " won't open.txt";
        }

        /// <summary>Builds the plain-text Gatekeeper note placed in the disk image.</summary>
        /// <param name="context">The publisher context.</param>
        /// <returns>The note text.</returns>
        public static string DmgNote(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            string name = context.Manifest.Project.Name;
            return string.Join("\n", new[]
            {
                name + " is free, open-source software and is not code-signed (signing certificates are an ongoing cost we choose not to pass on).",
                "macOS may say: \"" + name + "\" Not Opened. Apple could not verify \"" + name + "\" is free of malware that may harm your Mac or compromise your privacy.",
                "That warning doesn't mean the download is damaged. To open " + name + " anyway:",
                "",
                "1. Drag " + name + " onto Applications, then open it. When the warning appears, click Done. Do not click Move to Trash.",
                "2. Open System Settings > Privacy & Security and scroll down to Security.",
                "3. Next to \"" + name + "\" was blocked to protect your Mac, click Open Anyway. The button only appears for about an hour after the blocked attempt; if it is missing, try to open " + name + " again first.",
                "4. Click Open Anyway again in the dialog that follows and enter your password. You only do this once.",
                "",
                "On macOS 15 and later, Control-click > Open no longer bypasses this prompt.",
                "",
                "Or, in Terminal, after copying the app to Applications:",
                "  xattr -dr com.apple.quarantine " + InstalledBundle(context),
                "",
                "The disk image does not add the " + context.PathCommand + " command to PATH. For that, install the .pkg instead, or run:",
                "  sudo mkdir -p " + context.Manifest.Macos.CommandDirectory + " && sudo ln -sf " + InstalledBundle(context) + "/Contents/MacOS/" + context.Cli.Executable + " " + context.Manifest.Macos.CommandDirectory + "/" + context.PathCommand,
                "",
                "To confirm the download is the one published, compare it with SHA256SUMS on the release page: " + context.Manifest.Project.RepositoryUrl + "/releases",
                ""
            });
        }

        /// <summary>Builds the entitlements used only when Developer ID signing is configured (hardened runtime for .NET).</summary>
        /// <returns>The entitlements property list.</returns>
        public static string Entitlements()
        {
            string[] keys = { "com.apple.security.cs.allow-jit", "com.apple.security.cs.allow-unsigned-executable-memory", "com.apple.security.cs.disable-library-validation" };
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n<plist version=\"1.0\">\n<dict>\n"
                + string.Concat(keys.Select(key => "  <key>" + key + "</key><true/>\n")) + "</dict>\n</plist>\n";
        }

        private static void Entry(StringBuilder plist, string key, string value) =>
            plist.Append("  <key>").Append(key).Append("</key><string>").Append(SecurityElement.Escape(value)).Append("</string>\n");

        private static string Html(string text) => SecurityElement.Escape(text) ?? "";

        private static string Shell(string value) => "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }
}
