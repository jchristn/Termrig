namespace Termrig.Publisher
{
    /// <summary>Product presentation shared by every installer format. Read from the <c>project</c> block of publisher.json.</summary>
    public sealed class ProjectInfo
    {
        /// <summary>Product name shown to users, for example <c>Termrig</c>.</summary>
        public string Name { get; set; } = "";

        /// <summary>Lowercase command and package name, for example <c>termrig</c>. Used in every file name.</summary>
        public string Command { get; set; } = "";

        /// <summary>
        /// Name of the command placed on PATH, for example <c>trig</c>. Defaults to <see cref="Command"/>. When it differs
        /// from the executable, Linux and macOS link it to the executable and Windows installs a <c>.cmd</c> shim.
        /// </summary>
        public string PathCommand { get; set; } = "";

        /// <summary>Returns <see cref="PathCommand"/>, or <see cref="Command"/> when it is unset.</summary>
        public string EffectivePathCommand => string.IsNullOrWhiteSpace(PathCommand) ? Command : PathCommand;

        /// <summary>Per-user data directory named in uninstall messages, for example <c>~/.termrig</c>. Optional.</summary>
        public string DataDirectory { get; set; } = "";

        /// <summary>Repository-relative PNG used for the macOS app icon instead of <see cref="Logo"/>. Optional.</summary>
        public string MacosIcon { get; set; } = "";

        /// <summary>Reverse-DNS identifier, for example <c>ai.latticetech.termrig</c>.</summary>
        public string Identifier { get; set; } = "";

        /// <summary>One-line tagline shown on welcome pages and as package summaries.</summary>
        public string Tagline { get; set; } = "";

        /// <summary>Longer paragraph describing the product.</summary>
        public string Description { get; set; } = "";

        /// <summary>SPDX license identifier, for example <c>MIT</c>.</summary>
        public string License { get; set; } = "";

        /// <summary>Repository-relative path of the license text.</summary>
        public string LicenseFile { get; set; } = "LICENSE.md";

        /// <summary>Repository-relative path of the square logo used on light backgrounds (PNG, transparent).</summary>
        public string Logo { get; set; } = "";

        /// <summary>Repository-relative path of the square logo used on dark backgrounds (PNG, transparent). Optional.</summary>
        public string LogoDark { get; set; } = "";

        /// <summary>Publisher shown to users, for example <c>Lattice AI, LLC</c>.</summary>
        public string Publisher { get; set; } = "";

        /// <summary>Publisher web site.</summary>
        public string PublisherUrl { get; set; } = "";

        /// <summary>Repository URL, used for links in installers and release notes.</summary>
        public string RepositoryUrl { get; set; } = "";

        /// <summary>Copyright line shown in bundle metadata.</summary>
        public string Copyright { get; set; } = "";

        /// <summary>Repository-relative MSBuild file that holds the single <c>Version</c> element.</summary>
        public string VersionFile { get; set; } = "Directory.Build.props";

        /// <summary>Repository-relative changelog with one <c>## [version]</c> section per release.</summary>
        public string Changelog { get; set; } = "CHANGELOG.md";

        /// <summary>Repository-relative release-notes template with <c>{version}</c> placeholders.</summary>
        public string InstallNotes { get; set; } = ".github/release-install-notes.md";
    }
}
