namespace Test.Publisher
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Formats.Tar;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Termrig.Publisher;
    using Touchstone.Core;

    /// <summary>Touchstone cases for the installer publisher.</summary>
    public static class PublisherCases
    {
        private const string Suite = "Publisher";

        /// <summary>Every publisher suite.</summary>
        public static IReadOnlyList<TestSuiteDescriptor> All => new[] { Build() };

        /// <summary>Builds the publisher suite.</summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Build()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case("CommittedManifestIsValid", CommittedManifest),
                Case("ManifestRejectsProblems", ManifestProblems),
                Case("RuntimeTargetsMapArchitectures", RuntimeTargets),
                Case("AssetNamesFollowFormatConventions", AssetNaming),
                Case("VersionValidationAndPrerelease", Versions),
                Case("HostChannelsInBuildOrder", HostChannels),
                Case("WindowsAppIdIsStable", AppIdStability),
                Case("InnoScriptHasPathLicenseAndStartup", InnoScript),
                Case("MacBundleAndInstallerMetadata", MacMetadata),
                Case("MacInstallScripts", MacScripts),
                Case("LinuxDesktopEntriesAndFpm", LinuxMetadata),
                Case("ChangelogSectionsAndNotes", ChangelogAndNotes),
                Case("ReleaseTagVerification", ReleaseVerification),
                Case("PngRoundTripAndLogoDecode", PngImages),
                Case("IconContainers", IconContainers),
                Case("ArtworkGeneration", Artwork),
                Case("PublishArguments", PublishArguments),
                Case("OptionsParsing", Options),
                CaseAsync("PortableArchives", ArchivesAsync),
                CaseAsync("ChecksumFile", ChecksumsAsync),
                CaseAsync("ReleaseNotesFile", NotesAsync),
                Case("DesktopAppManifestRules", DesktopManifestRules),
                Case("DesktopAppInstallerMetadata", DesktopMetadata),
                CaseAsync("DesktopAppArchivesShipExtras", DesktopArchivesAsync)
            };
            if (!OperatingSystem.IsWindows())
            {
                cases.Add(Case("LinuxStagingLayout", LinuxStaging));
                cases.Add(Case("DesktopAppLinuxStaging", DesktopLinuxStaging));
            }
            return new TestSuiteDescriptor(Suite, "Installer manifest, packaging templates, artwork, archives, and release checks", cases);
        }

        private static TestCaseDescriptor Case(string name, Action execute) => new TestCaseDescriptor(Suite, name, name,
            cancellationToken => { cancellationToken.ThrowIfCancellationRequested(); execute(); return Task.CompletedTask; });

        private static TestCaseDescriptor CaseAsync(string name, Func<CancellationToken, Task> execute) => new TestCaseDescriptor(Suite, name, name, execute);

        private static void CommittedManifest()
        {
            string root = ManifestLoader.FindRoot(AppContext.BaseDirectory);
            PublisherManifest manifest = ManifestLoader.Load(Path.Combine(root, "publisher.json"));
            Fixture.Check(manifest.Project.Identifier == "ai.latticetech.termrig", "Identifier must be ai.latticetech.termrig.");
            Fixture.Check(manifest.Project.Publisher == "Lattice AI, LLC" && manifest.Project.PublisherUrl == "https://latticetech.ai", "Publisher must be Lattice AI, LLC.");
            Fixture.Check(manifest.Artifacts.Count == 1 && manifest.Artifacts[0].Kind == "desktop" && manifest.Artifacts[0].Executable == "Termrig", "Termrig ships one desktop executable.");
            Fixture.Check(manifest.Artifacts[0].Properties.TryGetValue("PackAsTool", out string? packAsTool) && packAsTool == "false", "Termrig.App packs as a dotnet tool; the installer build must turn that off.");
            Fixture.Check(manifest.Project.Command == "termrig" && manifest.Project.EffectivePathCommand == "trig", "Files are named termrig and the command on PATH is trig.");
            Fixture.Check(manifest.LaunchEnvironment.TryGetValue("TERMRIG_FOREGROUND", out string? foreground) && foreground == "1", "Launchers must keep Termrig in the foreground so the Dock and AppImage track it.");
            foreach (string rid in new[] { "win-x64", "win-arm64", "osx-x64", "osx-arm64", "linux-x64", "linux-arm64" })
                Fixture.Check(manifest.Runtimes.Contains(rid), "Runtime matrix is missing " + rid + ".");
            Fixture.Check(!manifest.Signing.Windows.IsConfigured && !manifest.Signing.Macos.IsConfigured, "Signing must stay off by policy.");
            foreach (string channel in new[] { "zip", "inno", "app", "tarball", "pkg", "dmg", "deb", "rpm", "appimage" })
                Fixture.Check(manifest.Channels.Contains(channel), "Channel " + channel + " is not enabled.");
            PublisherContext context = new PublisherContext(manifest, root, VersionReader.Read(Path.Combine(root, manifest.Project.VersionFile)), null, null);
            foreach (string path in new[] { manifest.Project.Logo, manifest.Project.MacosIcon, manifest.Project.LicenseFile, manifest.Project.InstallNotes, manifest.Project.Changelog })
                Fixture.Check(File.Exists(context.Resolve(path)), "Manifest path is missing: " + path);
            foreach (ArtifactInfo artifact in manifest.Artifacts)
                Fixture.Check(File.Exists(context.Resolve(artifact.Project)), "Artifact project is missing: " + artifact.Project);
            Fixture.Check(File.ReadAllText(context.Resolve(manifest.Project.InstallNotes)).Contains("{version}", StringComparison.Ordinal), "Install notes need {version} placeholders.");
            Fixture.Check(File.ReadAllText(context.Resolve(manifest.Project.Changelog)).Contains("## [Unreleased]", StringComparison.Ordinal), "Changelog needs an Unreleased section.");
            RgbaImage logo = PngCodec.Decode(File.ReadAllBytes(context.Resolve(manifest.Project.Logo)));
            Fixture.Check(logo.Width == logo.Height, "The installer logo must be square.");
        }

        private static void ManifestProblems()
        {
            Fixture.Throws<PublisherException>(() => ManifestLoader.Parse("{ not json"));
            Fixture.Throws<PublisherException>(() => ManifestLoader.Parse("{ \"unexpected\": true }"));
            try
            {
                ManifestLoader.Parse("{ \"project\": { \"name\": \"X\" }, \"runtimes\": [ \"freebsd-x64\" ] }");
                throw new InvalidOperationException("An incomplete manifest was accepted.");
            }
            catch (PublisherException error)
            {
                foreach (string expected in new[] { "project.command", "project.tagline", "freebsd-x64", "artifacts", "framework" })
                    Fixture.Check(error.Message.Contains(expected, StringComparison.Ordinal), "Validation did not report " + expected + ".");
            }
            string twoClis = PublisherFixture.ManifestJson.Replace("\"kind\": \"tray\"", "\"kind\": \"cli\"", StringComparison.Ordinal);
            Fixture.Throws<PublisherException>(() => ManifestLoader.Parse(twoClis));
            string caseClash = PublisherFixture.ManifestJson.Replace("Sample.Desktop", "SAMPLE", StringComparison.Ordinal);
            Fixture.Throws<PublisherException>(() => ManifestLoader.Parse(caseClash));
            string badChannel = PublisherFixture.ManifestJson.Replace("\"zip\",", "\"msix\",", StringComparison.Ordinal);
            Fixture.Throws<PublisherException>(() => ManifestLoader.Parse(badChannel));
            Fixture.Check(ManifestLoader.Parse(PublisherFixture.ManifestJson).Artifacts.Count == 2, "The synthetic manifest should parse.");
        }

        private static void RuntimeTargets()
        {
            RuntimeTarget linuxArm = RuntimeTarget.Parse("linux-arm64");
            Fixture.Check(linuxArm.OperatingSystem == "linux" && linuxArm.DebianArchitecture == "arm64" && linuxArm.GnuArchitecture == "aarch64", "linux-arm64 mapping is wrong.");
            RuntimeTarget linuxX64 = RuntimeTarget.Parse("linux-x64");
            Fixture.Check(linuxX64.DebianArchitecture == "amd64" && linuxX64.GnuArchitecture == "x86_64", "linux-x64 mapping is wrong.");
            RuntimeTarget windows = RuntimeTarget.Parse("win-x64");
            Fixture.Check(windows.ExecutableSuffix == ".exe" && windows.InnoArchitecture == "x64compatible", "win-x64 mapping is wrong.");
            Fixture.Check(RuntimeTarget.Parse("win-arm64").InnoArchitecture == "arm64", "win-arm64 must install only on ARM64.");
            Fixture.Check(RuntimeTarget.Parse("osx-arm64").MacArchitecture == "arm64" && RuntimeTarget.Parse("osx-x64").MacArchitecture == "x86_64", "macOS mapping is wrong.");
            foreach (string invalid in new[] { "linux-x86", "android-arm64", "win", "osx-arm64-extra" })
                Fixture.Throws<PublisherException>(() => RuntimeTarget.Parse(invalid));
        }

        private static void AssetNaming()
        {
            Dictionary<string, string> expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zip|win-x64"] = "termrig-0.1.0-win-x64.zip",
                ["inno|win-arm64"] = "termrig-0.1.0-win-arm64-setup.exe",
                ["tarball|osx-arm64"] = "termrig-0.1.0-osx-arm64.tar.gz",
                ["pkg|osx-x64"] = "termrig-0.1.0-osx-x64.pkg",
                ["dmg|osx-arm64"] = "termrig-0.1.0-osx-arm64.dmg",
                ["deb|linux-x64"] = "termrig_0.1.0_amd64.deb",
                ["deb|linux-arm64"] = "termrig_0.1.0_arm64.deb",
                ["rpm|linux-x64"] = "termrig-0.1.0-1.x86_64.rpm",
                ["rpm|linux-arm64"] = "termrig-0.1.0-1.aarch64.rpm",
                ["appimage|linux-x64"] = "termrig-0.1.0-x86_64.AppImage",
                ["appimage|linux-arm64"] = "termrig-0.1.0-aarch64.AppImage"
            };
            foreach (KeyValuePair<string, string> pair in expected)
            {
                string[] parts = pair.Key.Split('|');
                string actual = AssetNames.For("termrig", "0.1.0", parts[0], RuntimeTarget.Parse(parts[1]));
                Fixture.Check(actual == pair.Value, "Expected " + pair.Value + " but got " + actual + ".");
            }
            Fixture.Check(AssetNames.For("termrig", "2.0.0-rc.1", "rpm", RuntimeTarget.Parse("linux-x64")) == "termrig-2.0.0~rc.1-1.x86_64.rpm", "RPM pre-release versions need a tilde.");
            Fixture.Throws<PublisherException>(() => AssetNames.For("termrig", "0.1.0", "publish", RuntimeTarget.Parse("linux-x64")));
            IReadOnlyList<string> all = AssetNames.Expected(ManifestLoader.Parse(PublisherFixture.ManifestJson), "1.2.3");
            Fixture.Check(all.Count == 19, "A full release has 19 files, not " + all.Count + ": " + string.Join(", ", all));
            Fixture.Check(all.Contains("SHA256SUMS") && all.Contains("sample-1.2.3-osx-arm64.tar.gz") && !all.Contains("sample-1.2.3-win-x64.tar.gz"), "Expected release files are wrong.");
        }

        private static void Versions()
        {
            foreach (string valid in new[] { "0.1.0", "1.4.0", "0.6.0-alpha", "2.0.0-rc.1" }) VersionReader.Validate(valid);
            foreach (string invalid in new[] { "1.4", "v1.4.0", "1.4.0.0", "", "1.4.0-" })
                Fixture.Throws<PublisherException>(() => VersionReader.Validate(invalid));
            Fixture.Check(VersionReader.IsPrerelease("0.1.0", true), "0.x releases are alpha pre-releases.");
            Fixture.Check(!VersionReader.IsPrerelease("0.1.0", false), "0.x can be marked stable when configured.");
            Fixture.Check(VersionReader.IsPrerelease("1.0.0-rc.1", false), "Suffixed versions are pre-releases.");
            Fixture.Check(!VersionReader.IsPrerelease("1.4.0", true), "1.4.0 is a stable release.");
            using PublisherFixture fixture = new PublisherFixture("3.2.1");
            Fixture.Check(VersionReader.Read(Path.Combine(fixture.Root, "Directory.Build.props")) == "3.2.1", "Version was not read from the props file.");
            File.WriteAllText(Path.Combine(fixture.Root, "Directory.Build.props"), "<Project />");
            Fixture.Throws<PublisherException>(() => VersionReader.Read(Path.Combine(fixture.Root, "Directory.Build.props")));
            Fixture.Throws<PublisherException>(() => VersionReader.Read(Path.Combine(fixture.Root, "missing.props")));
        }

        private static void HostChannels()
        {
            PublisherManifest manifest = ManifestLoader.Parse(PublisherFixture.ManifestJson);
            Fixture.Check(string.Join(",", ChannelCatalog.ForHost(manifest, "windows")) == "publish,zip,inno", "Windows channels are wrong.");
            Fixture.Check(string.Join(",", ChannelCatalog.ForHost(manifest, "macos")) == "publish,app,tarball,pkg,dmg", "macOS channels are wrong.");
            Fixture.Check(string.Join(",", ChannelCatalog.ForHost(manifest, "linux")) == "publish,tarball,deb,rpm,appimage", "Linux channels are wrong.");
            manifest.Channels.Remove("dmg");
            Fixture.Check(!ChannelCatalog.ForHost(manifest, "macos").Contains("dmg"), "A disabled channel was scheduled.");
            Fixture.Throws<PublisherException>(() => ChannelCatalog.OperatingSystem("msix"));
        }

        private static void AppIdStability()
        {
            Fixture.Check(DeterministicGuid.Create("http://python.org/") == new Guid("4c565f0d-3f5a-5890-b41b-20cf47701c5e"), "Version 5 GUIDs do not match RFC 4122.");
            Fixture.Check(DeterministicGuid.Create("ai.latticetech.termrig") == new Guid("acca3305-16ea-5300-8308-fd5570d19d31"), "Termrig's AppId changed; Windows would install upgrades side by side.");
            Fixture.Check(InnoScriptWriter.AppId("ai.latticetech.termrig") == "{{ACCA3305-16EA-5300-8308-FD5570D19D31}", "Inno AppId escaping is wrong.");
        }

        private static void InnoScript()
        {
            using PublisherFixture fixture = new PublisherFixture();
            PublisherContext context = fixture.Context();
            string script = InnoScriptWriter.Build(context, RuntimeTarget.Parse("win-arm64"), @"C:\payload", @"C:\work\LICENSE.txt");
            Fixture.Check(!script.Replace("\r\n", "", StringComparison.Ordinal).Contains('\n', StringComparison.Ordinal), "Inno scripts must use CRLF.");
            foreach (string expected in new[]
            {
                "AppId={{", "AppName=Sample App", "AppPublisher=Lattice AI, LLC", "ChangesEnvironment=yes", "LicenseFile=C:\\work\\LICENSE.txt",
                "ArchitecturesAllowed=arm64", "ArchitecturesInstallIn64BitMode=arm64", "OutputBaseFilename=sample-1.2.3-win-arm64-setup", "PrivilegesRequired=admin",
                "SetupIconFile=", "WizardImageFile=", "WizardSmallImageFile=", "UninstallDisplayIcon={app}\\Sample App.ico",
                "WelcomeLabel2=A synthetic tagline.", "the sample command to PATH", "ValueName: \"Path\"", "NeedsAddPath(ExpandConstant('{app}'))",
                "ValueName: \"Sample App Agent\"", "\"\"\"{app}\\Sample.Desktop.exe\"\" --tray\"", "Tasks: startup", "RemovePath(ExpandConstant('{app}'))",
                "Source: \"" + Path.Combine(@"C:\payload", "sample.exe") + "\"", "Source: \"" + Path.Combine(@"C:\payload", "Sample.Desktop.exe") + "\"", "Parameters: \"desktop-stop\"", "runasoriginaluser"
            })
                Fixture.Check(script.Contains(expected, StringComparison.Ordinal), "Inno script is missing: " + expected);
            Fixture.Check(script.Split("WizardImageFile=")[1].Split("\r\n")[0].Split(',').Length == 3, "Wizard images need 100, 150, and 200 percent variants.");
            Fixture.Check(InnoScriptWriter.NumericVersion("2.0.0-rc.1") == "2.0.0.0", "Numeric version must drop the suffix.");
            Fixture.Check(InnoScriptWriter.Build(context, RuntimeTarget.Parse("win-x64"), @"C:\p", @"C:\l.txt").Contains("ArchitecturesAllowed=x64compatible", StringComparison.Ordinal), "x64 architecture is wrong.");
        }

        private static void MacMetadata()
        {
            using PublisherFixture fixture = new PublisherFixture();
            PublisherContext context = fixture.Context();
            XDocument info = XDocument.Parse(MacPackageWriter.InfoPlist(context));
            List<string> values = info.Descendants("string").Select(element => element.Value).ToList();
            foreach (string expected in new[] { "ai.latticetech.sample", "Sample App", "Sample.Desktop", "1.2.3", ArtworkBuilder.IcnsFile, "APPL" })
                Fixture.Check(values.Contains(expected), "Info.plist is missing " + expected + ".");
            XDocument distribution = XDocument.Parse(MacPackageWriter.Distribution(context, RuntimeTarget.Parse("osx-arm64"), "sample-component.pkg"));
            XElement gui = distribution.Root!;
            Fixture.Check(gui.Element("title")!.Value == "Sample App", "Installer title must be the product name.");
            Fixture.Check(gui.Element("license")!.Attribute("file")!.Value == "LICENSE.txt", "Installer must show the license.");
            Fixture.Check(gui.Element("welcome") != null && gui.Element("background") != null && gui.Element("background-darkAqua") != null, "Installer needs welcome and both backgrounds.");
            Fixture.Check(gui.Element("background")!.Attribute("scaling")!.Value == "none" && gui.Element("background")!.Attribute("alignment")!.Value == "bottomleft", "Background must not be stretched.");
            Fixture.Check(gui.Element("options")!.Attribute("hostArchitectures")!.Value == "arm64", "arm64 package must require Apple Silicon.");
            Fixture.Check(gui.Element("pkg-ref")!.Value == "sample-component.pkg", "Distribution must reference the component package.");
            XDocument component = XDocument.Parse(MacPackageWriter.ComponentPlist(context));
            Fixture.Check(component.Descendants("key").Any(key => key.Value == "BundleIsRelocatable" && ((XElement)key.NextNode!).Name == "false"), "Bundle must not be relocatable.");
            string welcome = MacPackageWriter.Welcome(context);
            Fixture.Check(welcome.Contains("A synthetic tagline.", StringComparison.Ordinal) && welcome.Contains("/usr/local/bin/sample", StringComparison.Ordinal), "Welcome page must state the tagline and the command.");
            XDocument.Parse(MacPackageWriter.Entitlements());
        }

        private static void MacScripts()
        {
            using PublisherFixture fixture = new PublisherFixture();
            PublisherContext context = fixture.Context();
            string postinstall = MacPackageWriter.Postinstall(context);
            Fixture.Check(postinstall.StartsWith("#!/bin/sh\n", StringComparison.Ordinal) && !postinstall.Contains('\r', StringComparison.Ordinal), "postinstall must be an LF shell script.");
            foreach (string expected in new[] { "mkdir -p '/usr/local/bin'", "ln -sf '/Applications/Sample App.app/Contents/MacOS/sample' '/usr/local/bin/sample'", "/Library/LaunchAgents/ai.latticetech.sample.agent.plist", "launchctl bootstrap", "launchctl bootout", "exit 0" })
                Fixture.Check(postinstall.Contains(expected, StringComparison.Ordinal), "postinstall is missing: " + expected);
            int start = postinstall.IndexOf("<'PLIST'\n", StringComparison.Ordinal) + "<'PLIST'\n".Length;
            int end = postinstall.IndexOf("PLIST\n", start, StringComparison.Ordinal);
            XDocument agent = XDocument.Parse(postinstall.Substring(start, end - start));
            List<string> strings = agent.Descendants("string").Select(element => element.Value).ToList();
            Fixture.Check(strings.Contains("ai.latticetech.sample.agent") && strings.Contains("/Applications/Sample App.app/Contents/MacOS/Sample.Desktop") && strings.Contains("--tray"), "LaunchAgent must start the tray app.");
            string uninstall = MacPackageWriter.Uninstall(context);
            foreach (string expected in new[] { "rm -rf '/Applications/Sample App.app'", "pkgutil --forget 'ai.latticetech.sample'", "rm -f '/Library/LaunchAgents/ai.latticetech.sample.agent.plist'" })
                Fixture.Check(uninstall.Contains(expected, StringComparison.Ordinal), "uninstall is missing: " + expected);
            string note = MacPackageWriter.DmgNote(context);
            foreach (string expected in new[] { "\"Sample App\" Not Opened", "click Done", "Privacy & Security", "Open Anyway", "Control-click > Open no longer bypasses", "xattr -dr com.apple.quarantine", "SHA256SUMS" })
                Fixture.Check(note.Contains(expected, StringComparison.Ordinal), "Disk image note is missing: " + expected);
            Fixture.Check(MacPackageWriter.DmgNoteName(context) == "If Sample App won't open.txt", "Disk image note name is wrong.");
        }

        private static void LinuxMetadata()
        {
            using PublisherFixture fixture = new PublisherFixture();
            PublisherContext context = fixture.Context();
            string menu = LinuxPackageWriter.DesktopEntry(context, "/opt/sample/Sample.Desktop", autostart: false);
            foreach (string expected in new[] { "Name=Sample App\n", "Comment=A synthetic tagline.\n", "Exec=/opt/sample/Sample.Desktop\n", "Icon=ai.latticetech.sample\n", "Terminal=false\n" })
                Fixture.Check(menu.Contains(expected, StringComparison.Ordinal), "Menu entry is missing: " + expected.Trim());
            Fixture.Check(!menu.Contains("--tray", StringComparison.Ordinal), "The menu entry must open the manager.");
            string autostart = LinuxPackageWriter.DesktopEntry(context, "/opt/my app/Sample.Desktop", autostart: true);
            Fixture.Check(autostart.Contains("Exec=\"/opt/my app/Sample.Desktop\" --tray\n", StringComparison.Ordinal), "Autostart must quote paths and pass --tray.");
            IReadOnlyList<string> deb = LinuxPackageWriter.FpmArguments(context, RuntimeTarget.Parse("linux-arm64"), "deb", "/stage", "/out/sample_1.2.3_arm64.deb");
            string debText = string.Join(" ", deb);
            foreach (string expected in new[] { "-t deb", "-n sample", "-v 1.2.3", "-a arm64", "--license MIT", "--vendor Lattice AI, LLC", "-d libicu74 | libicu72", "-d libx11-6", "-C /stage" })
                Fixture.Check(debText.Contains(expected, StringComparison.Ordinal), "deb arguments are missing: " + expected);
            string rpmText = string.Join(" ", LinuxPackageWriter.FpmArguments(context, RuntimeTarget.Parse("linux-x64"), "rpm", "/stage", "/out/x.rpm"));
            foreach (string expected in new[] { "-t rpm", "-a x86_64", "--iteration 1", "-d libicu" })
                Fixture.Check(rpmText.Contains(expected, StringComparison.Ordinal), "rpm arguments are missing: " + expected);
            Fixture.Throws<PublisherException>(() => LinuxPackageWriter.FpmArguments(context, RuntimeTarget.Parse("linux-x64"), "apk", "/s", "/o"));
            string appRun = LinuxPackageWriter.AppRun(context);
            Fixture.Check(appRun.StartsWith("#!/bin/sh\n", StringComparison.Ordinal) && appRun.Contains("usr/bin/sample", StringComparison.Ordinal) && appRun.Contains("usr/bin/Sample.Desktop", StringComparison.Ordinal), "AppRun must start the tray app and offer the CLI.");
        }

        private static void ChangelogAndNotes()
        {
            string changelog = "# Changelog\r\n\r\n## [Unreleased]\r\n\r\n## [1.4.0] - 2026-11-20\r\n\r\n- Added installers.\r\n\r\n## [1.3.0] - 2026-10-01\r\n\r\n- Older.\r\n";
            Fixture.Check(ChangelogReader.Section(changelog, "1.4.0") == "- Added installers.", "Changelog section was not extracted.");
            Fixture.Check(ChangelogReader.Section(changelog, "1.3.0") == "- Older.", "Last changelog section was not extracted.");
            Fixture.Throws<PublisherException>(() => ChangelogReader.Section(changelog, "9.9.9"));
            Fixture.Throws<PublisherException>(() => ChangelogReader.Section(changelog, "Unreleased"));
            Fixture.Throws<PublisherException>(() => ChangelogReader.Section("## [1.4.0]\n\n## [1.3.0]\n- x\n", "1.4.0"));
            string notes = ReleaseNotesWriter.Compose("## Installing\n\n`app-{version}.pkg` and `app_{version}_amd64.deb`\n", "1.4.0", "- Added installers.");
            Fixture.Check(!notes.Contains("{version}", StringComparison.Ordinal), "A {version} placeholder survived.");
            Fixture.Check(notes.IndexOf("## Installing", StringComparison.Ordinal) < notes.IndexOf("## What's changed", StringComparison.Ordinal), "Install steps must come before the changelog.");
            Fixture.Check(notes.Contains("`app-1.4.0.pkg`", StringComparison.Ordinal) && notes.EndsWith("- Added installers.\n", StringComparison.Ordinal), "Notes composition is wrong.");
            Fixture.Check(ReleaseNotesWriter.Title("Termrig", "0.1.0") == "Termrig 0.1.0", "Release title must be '<App> <version>'.");
        }

        private static void ReleaseVerification()
        {
            using PublisherFixture fixture = new PublisherFixture();
            PublisherContext context = fixture.Context();
            ReleaseChannel.Verify(context, "v1.2.3");
            Fixture.Throws<PublisherException>(() => ReleaseChannel.Verify(context, "1.2.3"));
            Fixture.Throws<PublisherException>(() => ReleaseChannel.Verify(context, "v1.2.4"));
            Fixture.Throws<PublisherException>(() => ReleaseChannel.VerifyAssets(context));
            Directory.CreateDirectory(context.OutputDirectory);
            foreach (string name in AssetNames.Expected(context.Manifest, context.Version)) File.WriteAllText(Path.Combine(context.OutputDirectory, name), "x");
            ReleaseChannel.VerifyAssets(context);
            File.Delete(Path.Combine(context.OutputDirectory, "sample_1.2.3_arm64.deb"));
            File.WriteAllText(Path.Combine(context.OutputDirectory, "stray.txt"), "x");
            try
            {
                ReleaseChannel.VerifyAssets(context);
                throw new InvalidOperationException("An incomplete release passed verification.");
            }
            catch (PublisherException error)
            {
                Fixture.Check(error.Message.Contains("MISSING: sample_1.2.3_arm64.deb", StringComparison.Ordinal) && error.Message.Contains("UNEXPECTED: stray.txt", StringComparison.Ordinal), "Verification must name each problem.");
            }
            File.WriteAllText(Path.Combine(fixture.Root, "CHANGELOG.md"), "# Changelog\n\n## [Unreleased]\n\n- Not released yet.\n");
            Fixture.Throws<PublisherException>(() => ReleaseChannel.Verify(context, "v1.2.3"));
        }

        private static void PngImages()
        {
            RgbaImage image = new RgbaImage(7, 5);
            for (int index = 0; index < image.Pixels.Length; index++) image.Pixels[index] = (byte)(index * 37 % 256);
            RgbaImage decoded = PngCodec.Decode(PngCodec.Encode(image));
            Fixture.Check(decoded.Width == 7 && decoded.Height == 5 && decoded.Pixels.AsSpan().SequenceEqual(image.Pixels), "PNG round trip changed pixels.");
            Fixture.Throws<PublisherException>(() => PngCodec.Decode(Encoding.ASCII.GetBytes("not a png file")));
            string root = ManifestLoader.FindRoot(AppContext.BaseDirectory);
            RgbaImage logo = PngCodec.Decode(File.ReadAllBytes(Path.Combine(root, "assets", "logo.png")));
            Fixture.Check(logo.Pixels.Where((value, index) => index % 4 == 3).Any(alpha => alpha == 0) && logo.Pixels.Where((value, index) => index % 4 == 3).Any(alpha => alpha == 255), "The logo should decode with transparent and opaque pixels.");
            RgbaImage small = logo.Resize(32, 32);
            RgbaImage large = small.Resize(100, 60);
            Fixture.Check(small.Width == 32 && large.Width == 100 && large.Height == 60, "Resizing produced the wrong dimensions.");
            RgbaImage canvas = RgbaImage.Filled(10, 10, 255, 255, 255, 255);
            canvas.Draw(RgbaImage.Filled(4, 4, 0, 0, 0, 255), 8, -2);
            Fixture.Check(canvas.Pixels[(1 * 10 + 9) * 4] == 0 && canvas.Pixels[(5 * 10 + 9) * 4] == 255, "Clipped drawing is wrong.");
        }

        private static void IconContainers()
        {
            RgbaImage source = RgbaImage.Filled(300, 300, 10, 20, 30, 255);
            byte[] ico = IconEncoder.Ico(source);
            Fixture.Check(BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(2)) == 1 && BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4)) == IconEncoder.IcoSizes.Count, "ICO header is wrong.");
            int lastOffset = BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(6 + 16 * (IconEncoder.IcoSizes.Count - 1) + 12));
            Fixture.Check(ico[lastOffset] == 137 && ico[lastOffset + 1] == (byte)'P', "The 256-pixel icon must be PNG.");
            int firstOffset = BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(6 + 12));
            Fixture.Check(BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(firstOffset)) == 40 && BinaryPrimitives.ReadInt32LittleEndian(ico.AsSpan(firstOffset + 8)) == 32, "Small icons must be 32-bit DIBs with doubled height.");
            byte[] icns = IconEncoder.Icns(source);
            Fixture.Check(Encoding.ASCII.GetString(icns, 0, 4) == "icns" && BinaryPrimitives.ReadInt32BigEndian(icns.AsSpan(4)) == icns.Length, "ICNS header is wrong.");
            Fixture.Check(Encoding.ASCII.GetString(icns, 8, 4) == "icp4", "ICNS must start with the 16-pixel entry.");
            byte[] bmp = IconEncoder.Bmp(RgbaImage.Filled(5, 3, 0, 0, 0, 0));
            Fixture.Check(bmp[0] == (byte)'B' && bmp[1] == (byte)'M' && BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(2)) == bmp.Length, "BMP header is wrong.");
            Fixture.Check(BinaryPrimitives.ReadInt32LittleEndian(bmp.AsSpan(18)) == 5 && BinaryPrimitives.ReadInt16LittleEndian(bmp.AsSpan(28)) == 24 && bmp[54] == 255, "BMP must be 24-bit with transparency composited on white.");
        }

        private static void Artwork()
        {
            using PublisherFixture fixture = new PublisherFixture();
            PublisherContext context = fixture.Context();
            ArtworkBuilder.Build(context);
            List<string> expected = new List<string> { ArtworkBuilder.IcoFile, ArtworkBuilder.IcnsFile, "pkg-background.png", "pkg-background-dark.png" };
            expected.AddRange(ArtworkBuilder.LinuxIconSizes.Select(size => "icon-" + size + ".png"));
            expected.AddRange(ArtworkBuilder.WizardImageWidths.Select(width => "wizard-" + width + ".bmp"));
            expected.AddRange(ArtworkBuilder.WizardSmallSizes.Select(size => "wizard-small-" + size + ".bmp"));
            foreach (string file in expected) Fixture.Check(File.Exists(Path.Combine(context.ArtworkDirectory, file)), "Artwork is missing " + file + ".");
            byte[] wizard = File.ReadAllBytes(Path.Combine(context.ArtworkDirectory, "wizard-164.bmp"));
            Fixture.Check(BinaryPrimitives.ReadInt32LittleEndian(wizard.AsSpan(18)) == 164 && BinaryPrimitives.ReadInt32LittleEndian(wizard.AsSpan(22)) == 314, "Inno wizard image must be 164x314.");
            RgbaImage icon = PngCodec.Decode(File.ReadAllBytes(Path.Combine(context.ArtworkDirectory, "icon-128.png")));
            Fixture.Check(icon.Width == 128 && icon.Height == 128, "Linux icon size is wrong.");
            File.Delete(Path.Combine(fixture.Root, "logo.png"));
            Fixture.Throws<PublisherException>(() => ArtworkBuilder.Build(context));
        }

        private static void PublishArguments()
        {
            using PublisherFixture fixture = new PublisherFixture();
            PublisherContext context = fixture.Context();
            ArtifactInfo cli = context.Cli;
            string windows = string.Join(" ", PublishChannel.Arguments(context, cli, RuntimeTarget.Parse("win-arm64"), "/out"));
            foreach (string expected in new[] { "publish", "-r win-arm64", "-f net10.0", "--self-contained true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:DebugType=none", "-p:Version=1.2.3", "-p:ApplicationIcon=" })
                Fixture.Check(windows.Contains(expected, StringComparison.Ordinal), "publish arguments are missing: " + expected);
            string linux = string.Join(" ", PublishChannel.Arguments(context, cli, RuntimeTarget.Parse("linux-x64"), "/out"));
            Fixture.Check(!linux.Contains("ApplicationIcon", StringComparison.Ordinal), "Only Windows builds embed the .ico.");
            Fixture.Throws<PublisherException>(() => fixture.Context(new[] { "freebsd-x64" }));
            Fixture.Check(fixture.Context(new[] { "linux-x64", "osx-arm64" }).TargetsFor("linux").Count == 1, "Target filtering is wrong.");
        }

        private static void Options()
        {
            PublisherOptions options = PublisherOptions.Parse(new[] { "--channel", "publish,zip", "--channel", "inno", "--rids", "win-x64, win-arm64", "--version", "1.0.0" });
            Fixture.Check(string.Join(",", options.Channels) == "publish,zip,inno", "Channels were not parsed in order.");
            Fixture.Check(options.Rids != null && string.Join(",", options.Rids) == "win-x64,win-arm64" && options.Version == "1.0.0", "Options were not parsed.");
            Fixture.Check(PublisherOptions.Parse(new[] { "--all" }).All && PublisherOptions.Parse(new[] { "--help" }).Help, "Flags were not parsed.");
            Fixture.Throws<PublisherException>(() => PublisherOptions.Parse(Array.Empty<string>()));
            Fixture.Throws<PublisherException>(() => PublisherOptions.Parse(new[] { "--channel" }));
            Fixture.Throws<PublisherException>(() => PublisherOptions.Parse(new[] { "--version", "--all" }));
            Fixture.Throws<PublisherException>(() => PublisherOptions.Parse(new[] { "--unknown" }));
        }

        private static async Task ArchivesAsync(CancellationToken cancellationToken)
        {
            using PublisherFixture fixture = new PublisherFixture();
            PublisherContext context = fixture.Context(new[] { "win-x64", "linux-arm64" });
            foreach (RuntimeTarget target in context.Targets) fixture.FakePayload(context, target);
            await ArchiveChannel.ZipAsync(context, cancellationToken).ConfigureAwait(false);
            await ArchiveChannel.TarballAsync(context, cancellationToken).ConfigureAwait(false);
            using (ZipArchive zip = ZipFile.OpenRead(Path.Combine(context.OutputDirectory, "sample-1.2.3-win-x64.zip")))
            {
                List<string> names = zip.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToList();
                Fixture.Check(string.Join(",", names) == "LICENSE.txt,Sample.Desktop.exe,sample.exe", "Zip contents are wrong: " + string.Join(",", names));
            }
            Fixture.Check(!File.Exists(Path.Combine(context.OutputDirectory, "sample-1.2.3-win-x64.tar.gz")), "Windows targets must not get a tarball.");
            await using FileStream file = File.OpenRead(Path.Combine(context.OutputDirectory, "sample-1.2.3-linux-arm64.tar.gz"));
            await using GZipStream gzip = new GZipStream(file, CompressionMode.Decompress);
            await using TarReader reader = new TarReader(gzip);
            Dictionary<string, UnixFileMode> modes = new Dictionary<string, UnixFileMode>(StringComparer.Ordinal);
            TarEntry? entry;
            while ((entry = await reader.GetNextEntryAsync(cancellationToken: cancellationToken).ConfigureAwait(false)) != null) modes[entry.Name] = entry.Mode;
            Fixture.Check(modes.Count == 3 && modes.ContainsKey("sample") && modes.ContainsKey("Sample.Desktop") && modes.ContainsKey("LICENSE.md"), "Tarball contents are wrong.");
            Fixture.Check(modes["sample"].HasFlag(UnixFileMode.OtherExecute) && !modes["LICENSE.md"].HasFlag(UnixFileMode.UserExecute), "Tarball modes are wrong.");
            PublisherContext missing = fixture.Context(new[] { "linux-x64" });
            await Fixture.ThrowsAsync<PublisherException>(() => ArchiveChannel.TarballAsync(missing, cancellationToken)).ConfigureAwait(false);
        }

        private static async Task ChecksumsAsync(CancellationToken cancellationToken)
        {
            using PublisherFixture fixture = new PublisherFixture();
            PublisherContext context = fixture.Context();
            await Fixture.ThrowsAsync<PublisherException>(() => ChecksumWriter.WriteAsync(context.OutputDirectory, cancellationToken)).ConfigureAwait(false);
            Directory.CreateDirectory(context.OutputDirectory);
            File.WriteAllText(Path.Combine(context.OutputDirectory, "b.deb"), "abc");
            File.WriteAllText(Path.Combine(context.OutputDirectory, "a.pkg"), "");
            await ChecksumWriter.WriteAsync(context.OutputDirectory, cancellationToken).ConfigureAwait(false);
            string sums = await ChecksumWriter.WriteAsync(context.OutputDirectory, cancellationToken).ConfigureAwait(false);
            Fixture.Check(sums == "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855  a.pkg\nba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  b.deb\n", "SHA256SUMS content is wrong: " + sums);
            Fixture.Check(File.ReadAllText(Path.Combine(context.OutputDirectory, ChecksumWriter.FileName)) == sums, "SHA256SUMS must not list itself.");
        }

        private static async Task NotesAsync(CancellationToken cancellationToken)
        {
            using PublisherFixture fixture = new PublisherFixture();
            PublisherContext context = fixture.Context();
            string output = Path.Combine(fixture.Root, "notes", "release.md");
            await ReleaseChannel.NotesAsync(context, output, cancellationToken).ConfigureAwait(false);
            string notes = File.ReadAllText(output);
            Fixture.Check(notes.StartsWith("## Installing", StringComparison.Ordinal) && notes.Contains("sample-1.2.3-win-x64-setup.exe", StringComparison.Ordinal), "Notes must start with install steps for this version.");
            Fixture.Check(notes.Contains("- Synthetic change.", StringComparison.Ordinal) && !notes.Contains("Older change", StringComparison.Ordinal), "Notes must hold only this version's changes.");
        }

        private static void LinuxStaging()
        {
            if (OperatingSystem.IsWindows()) return;
            using PublisherFixture fixture = new PublisherFixture();
            PublisherContext context = fixture.Context(new[] { "linux-x64" });
            RuntimeTarget target = context.Targets[0];
            fixture.FakePayload(context, target);
            ArtworkBuilder.Build(context);
            string root = LinuxChannel.Stage(context, target);
            FileInfo link = new FileInfo(Path.Combine(root, "usr", "bin", "sample"));
            Fixture.Check(link.LinkTarget == "/opt/sample/sample", "The command symlink must point into the install directory.");
            Fixture.Check(File.GetUnixFileMode(Path.Combine(root, "opt", "sample", "Sample.Desktop")).HasFlag(UnixFileMode.OtherExecute), "Executables must be 0755.");
            Fixture.Check(File.ReadAllText(Path.Combine(root, "etc", "xdg", "autostart", "ai.latticetech.sample.desktop")).Contains("--tray", StringComparison.Ordinal), "Autostart entry is missing.");
            Fixture.Check(File.Exists(Path.Combine(root, "usr", "share", "applications", "ai.latticetech.sample.desktop")), "Menu entry is missing.");
            foreach (int size in new[] { 48, 128, 256 })
                Fixture.Check(File.Exists(Path.Combine(root, "usr", "share", "icons", "hicolor", size + "x" + size, "apps", "ai.latticetech.sample.png")), "Icon " + size + " is missing.");
            Fixture.Check(File.ReadAllText(Path.Combine(root, "usr", "share", "doc", "sample", "copyright")).StartsWith("MIT License\n", StringComparison.Ordinal), "Debian copyright must hold the LF-normalized license.");
        }
            private static void DesktopManifestRules()
        {
            PublisherManifest manifest = ManifestLoader.Parse(PublisherFixture.DesktopManifestJson);
            Fixture.Check(manifest.Project.EffectivePathCommand == "dapp" && ManifestLoader.Parse(PublisherFixture.ManifestJson).Project.EffectivePathCommand == "sample", "pathCommand must default to command.");
            string noEntry = PublisherFixture.DesktopManifestJson.Replace("\"kind\": \"desktop\"", "\"kind\": \"tray\"", StringComparison.Ordinal);
            Fixture.Throws<PublisherException>(() => ManifestLoader.Parse(noEntry));
            string badKind = PublisherFixture.DesktopManifestJson.Replace("\"kind\": \"desktop\"", "\"kind\": \"gui\"", StringComparison.Ordinal);
            Fixture.Throws<PublisherException>(() => ManifestLoader.Parse(badKind));
            string badAlias = PublisherFixture.DesktopManifestJson.Replace("\"pathCommand\": \"dapp\"", "\"pathCommand\": \"D App\"", StringComparison.Ordinal);
            Fixture.Throws<PublisherException>(() => ManifestLoader.Parse(badAlias));
            string badEnvironment = PublisherFixture.DesktopManifestJson.Replace("\"DESKTOP_FOREGROUND\": \"1\"", "\"DESKTOP_FOREGROUND\": \"1; rm\"", StringComparison.Ordinal);
            Fixture.Throws<PublisherException>(() => ManifestLoader.Parse(badEnvironment));
            using PublisherFixture fixture = new PublisherFixture(desktop: true);
            PublisherContext context = fixture.Context();
            Fixture.Check(context.Cli.Executable == "DesktopApp" && context.Main.Executable == "DesktopApp" && context.Tray == null && context.HasMenuEntry, "A desktop artifact is the PATH command, the main executable, and the menu entry.");
            Fixture.Check(context.PathCommand == "dapp" && context.PathCommandIsAlias, "The PATH alias was not resolved.");
            string arguments = string.Join(" ", PublishChannel.Arguments(context, context.Desktop!, RuntimeTarget.Parse("osx-arm64"), "/out"));
            Fixture.Check(arguments.Contains("-p:PackAsTool=false", StringComparison.Ordinal), "Artifact properties must reach dotnet publish.");
        }

        private static void DesktopMetadata()
        {
            using PublisherFixture fixture = new PublisherFixture(desktop: true);
            PublisherContext context = fixture.Context();
            RuntimeTarget windows = RuntimeTarget.Parse("win-x64");
            fixture.FakePayload(context, windows);
            string plist = MacPackageWriter.InfoPlist(context);
            XDocument info = XDocument.Parse(plist);
            Fixture.Check(info.Descendants("key").Any(key => key.Value == "CFBundleExecutable" && ((XElement)key.NextNode!).Value == "DesktopApp"), "The bundle must launch the desktop app.");
            Fixture.Check(info.Descendants("key").Any(key => key.Value == "LSEnvironment") && plist.Contains("<key>DESKTOP_FOREGROUND</key><string>1</string>", StringComparison.Ordinal), "Info.plist needs the launch environment.");
            Fixture.Check(!MacPackageWriter.InfoPlist(new PublisherFixture().Context()).Contains("LSEnvironment", StringComparison.Ordinal), "No launch environment means no LSEnvironment.");
            string postinstall = MacPackageWriter.Postinstall(context);
            Fixture.Check(postinstall.Contains("ln -sf '/Applications/Desktop App.app/Contents/MacOS/DesktopApp' '/usr/local/bin/dapp'", StringComparison.Ordinal), "postinstall must link the PATH alias.");
            Fixture.Check(!postinstall.Contains("LaunchAgents", StringComparison.Ordinal), "A desktop app does not start at login.");
            string uninstall = MacPackageWriter.Uninstall(context);
            Fixture.Check(uninstall.Contains("'/usr/local/bin/dapp'", StringComparison.Ordinal) && uninstall.Contains("~/.dapp is kept", StringComparison.Ordinal), "uninstall must remove the alias and name the data directory.");
            Fixture.Check(MacPackageWriter.Welcome(context).Contains("/usr/local/bin/dapp", StringComparison.Ordinal) && MacPackageWriter.DmgNote(context).Contains("/usr/local/bin/dapp", StringComparison.Ordinal), "Installer text must name the alias.");
            string script = InnoScriptWriter.Build(context, windows, @"C:\payload", @"C:\work\LICENSE.txt");
            foreach (string expected in new[]
            {
                "Source: \"" + Path.Combine(Path.GetDirectoryName(@"C:\work\LICENSE.txt") ?? "", "dapp.cmd") + "\"; DestDir: \"{app}\"", "DestDir: \"{app}\\Licenses\"", "adds a Start menu shortcut and the dapp command to PATH",
                "Name: \"{autoprograms}\\Desktop App\"; Filename: \"{app}\\DesktopApp.exe\"", "{cm:LaunchProgram,Desktop App}"
            })
                Fixture.Check(script.Contains(expected, StringComparison.Ordinal), "Inno script is missing: " + expected);
            Fixture.Check(!script.Contains("Tasks: startup", StringComparison.Ordinal) && !script.Contains("desktop-stop", StringComparison.Ordinal), "A desktop app has no startup task.");
            string shim = InnoScriptWriter.CommandShim(context, windows);
            Fixture.Check(shim.Contains("\"%~dp0DesktopApp.exe\" %*\r\n", StringComparison.Ordinal) && InnoScriptWriter.ShimFile(context) == "dapp.cmd", "The Windows shim must forward every argument to the executable beside it.");
            string menu = LinuxPackageWriter.DesktopEntry(context, "/opt/dapp/DesktopApp", autostart: false);
            Fixture.Check(menu.Contains("Exec=env DESKTOP_FOREGROUND=1 /opt/dapp/DesktopApp\n", StringComparison.Ordinal), "The menu entry must start with the launch environment.");
            Fixture.Check(LinuxPackageWriter.DesktopEntry(context, "DesktopApp", autostart: false, launchEnvironment: false).Contains("Exec=DesktopApp\n", StringComparison.Ordinal), "The AppImage entry names the bare executable.");
            string appRun = LinuxPackageWriter.AppRun(context);
            Fixture.Check(appRun.Contains("export DESKTOP_FOREGROUND=1\n", StringComparison.Ordinal) && appRun.Contains("exec \"$here/usr/bin/DesktopApp\" \"$@\"", StringComparison.Ordinal) && !appRun.Contains("--cli", StringComparison.Ordinal), "AppRun must export the launch environment and run the app.");
            Fixture.Check(string.Join(",", context.PayloadExtras(windows)) == "Licenses/Font-OFL.txt", "Payload extras are wrong: " + string.Join(",", context.PayloadExtras(windows)));
        }

        private static async Task DesktopArchivesAsync(CancellationToken cancellationToken)
        {
            using PublisherFixture fixture = new PublisherFixture(desktop: true);
            PublisherContext context = fixture.Context(new[] { "win-arm64", "osx-arm64" });
            foreach (RuntimeTarget target in context.Targets) fixture.FakePayload(context, target);
            await ArchiveChannel.ZipAsync(context, cancellationToken).ConfigureAwait(false);
            await ArchiveChannel.TarballAsync(context, cancellationToken).ConfigureAwait(false);
            using (ZipArchive zip = ZipFile.OpenRead(Path.Combine(context.OutputDirectory, "dapp-1.2.3-win-arm64.zip")))
            {
                string names = string.Join(",", zip.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal));
                Fixture.Check(names == "DesktopApp.exe,LICENSE.txt,Licenses/Font-OFL.txt,dapp.cmd", "Zip contents are wrong: " + names);
            }
            await using FileStream file = File.OpenRead(Path.Combine(context.OutputDirectory, "dapp-1.2.3-osx-arm64.tar.gz"));
            await using GZipStream gzip = new GZipStream(file, CompressionMode.Decompress);
            await using TarReader reader = new TarReader(gzip);
            List<string> entries = new List<string>();
            TarEntry? entry;
            while ((entry = await reader.GetNextEntryAsync(cancellationToken: cancellationToken).ConfigureAwait(false)) != null) entries.Add(entry.Name);
            Fixture.Check(string.Join(",", entries.OrderBy(name => name, StringComparer.Ordinal)) == "DesktopApp,LICENSE.md,Licenses/Font-OFL.txt", "Tarball contents are wrong: " + string.Join(",", entries));
        }

        private static void DesktopLinuxStaging()
        {
            if (OperatingSystem.IsWindows()) return;
            using PublisherFixture fixture = new PublisherFixture(desktop: true);
            PublisherContext context = fixture.Context(new[] { "linux-arm64" });
            RuntimeTarget target = context.Targets[0];
            fixture.FakePayload(context, target);
            ArtworkBuilder.Build(context);
            string root = LinuxChannel.Stage(context, target);
            Fixture.Check(new FileInfo(Path.Combine(root, "usr", "bin", "dapp")).LinkTarget == "/opt/dapp/DesktopApp", "The alias must link to the desktop executable.");
            Fixture.Check(File.Exists(Path.Combine(root, "opt", "dapp", "Licenses", "Font-OFL.txt")), "Payload extras must ship in the install directory.");
            Fixture.Check(File.ReadAllText(Path.Combine(root, "usr", "share", "applications", "ai.latticetech.dapp.desktop")).Contains("Exec=env DESKTOP_FOREGROUND=1 /opt/dapp/DesktopApp\n", StringComparison.Ordinal), "The menu entry is missing or wrong.");
            Fixture.Check(!Directory.Exists(Path.Combine(root, "etc")), "A desktop app does not autostart, so nothing is staged under /etc.");
            IReadOnlyList<string> fpm = LinuxPackageWriter.FpmArguments(context, target, "deb", root, "/out/dapp.deb");
            Fixture.Check(fpm[^2] == "opt" && fpm[^1] == "usr", "fpm must package only the staged directories: " + string.Join(" ", fpm.TakeLast(3)));
        }
}
}
