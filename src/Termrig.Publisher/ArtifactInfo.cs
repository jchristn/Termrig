namespace Termrig.Publisher
{
    using System.Collections.Generic;

    /// <summary>One executable built for every runtime identifier and shipped in every installer.</summary>
    public sealed class ArtifactInfo
    {
        /// <summary>Short artifact name used for staging directories, for example <c>cli</c>.</summary>
        public string Name { get; set; } = "";

        /// <summary>Repository-relative project file.</summary>
        public string Project { get; set; } = "";

        /// <summary>Executable file name without the Windows <c>.exe</c> extension.</summary>
        public string Executable { get; set; } = "";

        /// <summary>
        /// Artifact kind: <c>cli</c> (placed on PATH), <c>tray</c> (menu entry and login startup), or <c>desktop</c>
        /// (a windowed app: the bundle's main executable and the menu entry, and placed on PATH when there is no cli).
        /// </summary>
        public string Kind { get; set; } = "";

        /// <summary>Extra MSBuild properties passed to <c>dotnet publish</c> as <c>-p:Name=Value</c>.</summary>
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
    }
}
