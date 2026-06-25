namespace Termrig.Core
{
    /// <summary>
    /// Shared constants for Termrig.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Public application name.
        /// </summary>
        public const string ApplicationName = "Termrig";

        /// <summary>
        /// Package publisher identity.
        /// </summary>
        public const string PublisherName = "jchristn";

        /// <summary>
        /// Stable reverse-DNS application identifier used by native packages.
        /// </summary>
        public const string ApplicationId = "com.jchristn.Termrig";

        /// <summary>
        /// Short application description used in package metadata.
        /// </summary>
        public const string ApplicationDescription = "Desktop terminal profile manager";

        /// <summary>
        /// Project homepage.
        /// </summary>
        public const string HomepageUrl = "https://github.com/jchristn/Termrig";

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
        /// Profile folder store filename.
        /// </summary>
        public const string ProfileFoldersFilename = "profile-folders.json";

        /// <summary>
        /// Color scheme store filename.
        /// </summary>
        public const string ColorSchemesFilename = "color-schemes.json";

        /// <summary>
        /// Workspace recovery store filename.
        /// </summary>
        public const string WorkspaceRecoveryFilename = "workspace-recovery.json";

        /// <summary>
        /// Preferred profile font for cmd.exe terminal tabs.
        /// </summary>
        public const string CmdTerminalFontFamily = "Cascadia Mono";

        /// <summary>
        /// Default terminal font size used when no tab or profile font size is set.
        /// </summary>
        public const double DefaultTerminalFontSize = 12;

        /// <summary>
        /// Default per-tab terminal scrollback buffer size.
        /// </summary>
        public const int DefaultTerminalBufferSize = 5000;

        /// <summary>
        /// Minimum per-tab terminal scrollback buffer size.
        /// </summary>
        public const int MinimumTerminalBufferSize = 1000;

        /// <summary>
        /// Maximum per-tab terminal scrollback buffer size.
        /// </summary>
        public const int MaximumTerminalBufferSize = 100000;

        /// <summary>
        /// Directory name under the application data directory used for terminal restore snapshots.
        /// </summary>
        public const string TerminalRestoreSubdirectoryName = "terminal-restore";

        /// <summary>
        /// Default number of recent terminal lines persisted for scrollback restore.
        /// </summary>
        public const int DefaultTerminalRestoreLineLimit = 1000;

        /// <summary>
        /// Minimum number of recent terminal lines persisted for scrollback restore.
        /// </summary>
        public const int MinimumTerminalRestoreLineLimit = 1;

        /// <summary>
        /// Maximum number of recent terminal lines persisted for scrollback restore.
        /// </summary>
        public const int MaximumTerminalRestoreLineLimit = MaximumTerminalBufferSize;
    }
}
