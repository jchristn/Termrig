namespace Termrig.Core
{
    /// <summary>
    /// Shared constants for Termrig.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Application data directory name created under the user's home directory.
        /// </summary>
        public const string ApplicationDirectoryName = ".termrig";

        /// <summary>
        /// Crash log subdirectory name.
        /// </summary>
        public const string CrashLogSubdirectoryName = "crashes";

        /// <summary>
        /// Profile store filename.
        /// </summary>
        public const string ProfilesFilename = "profiles.json";

        /// <summary>
        /// Color scheme store filename.
        /// </summary>
        public const string ColorSchemesFilename = "color-schemes.json";

        /// <summary>
        /// Default terminal font size used when no tab or profile font size is set.
        /// </summary>
        public const double DefaultTerminalFontSize = 12;
    }
}
