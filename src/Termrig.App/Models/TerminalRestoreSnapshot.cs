namespace Termrig.App.Models
{
    using System;
    using XTerm.Restore;

    /// <summary>
    /// Persisted terminal scrollback restore snapshot with profile/tab metadata.
    /// </summary>
    public sealed class TerminalRestoreSnapshot
    {
        /// <summary>
        /// Snapshot schema version.
        /// </summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>
        /// UTC time when the snapshot was captured.
        /// </summary>
        public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Saved profile identifier.
        /// </summary>
        public string ProfileId { get; set; } = String.Empty;

        /// <summary>
        /// Saved profile display name at capture time.
        /// </summary>
        public string ProfileName { get; set; } = String.Empty;

        /// <summary>
        /// Saved tab identifier.
        /// </summary>
        public string TabId { get; set; } = String.Empty;

        /// <summary>
        /// Saved tab display name at capture time.
        /// </summary>
        public string TabName { get; set; } = String.Empty;

        /// <summary>
        /// Configured or detected working directory at capture time.
        /// </summary>
        public string WorkingDirectory { get; set; } = String.Empty;

        /// <summary>
        /// Configured restore line limit.
        /// </summary>
        public int ScrollbackLineLimit { get; set; }

        /// <summary>
        /// Captured terminal buffer state.
        /// </summary>
        public TerminalBufferSnapshot Buffer { get; set; } = new TerminalBufferSnapshot();
    }
}
