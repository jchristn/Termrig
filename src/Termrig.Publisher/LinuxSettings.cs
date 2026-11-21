namespace Termrig.Publisher
{
    using System.Collections.Generic;

    /// <summary>Linux package metadata.</summary>
    public sealed class LinuxSettings
    {
        /// <summary>Install directory for the executables, for example <c>/opt/termrig</c>.</summary>
        public string InstallDirectory { get; set; } = "";

        /// <summary>Debian package section.</summary>
        public string Category { get; set; } = "utils";

        /// <summary>Freedesktop menu categories, separated by semicolons.</summary>
        public string DesktopCategories { get; set; } = "Utility;";

        /// <summary>Debian runtime dependencies in control-file syntax (alternatives separated by <c>|</c>).</summary>
        public List<string> DebDependencies { get; set; } = new List<string>();

        /// <summary>RPM runtime dependencies.</summary>
        public List<string> RpmDependencies { get; set; } = new List<string>();
    }
}
