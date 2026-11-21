namespace Termrig.Publisher
{
    /// <summary>Per-operating-system signing switches. Both are off by policy.</summary>
    public sealed class SigningSettings
    {
        /// <summary>Windows Authenticode signing.</summary>
        public SigningTarget Windows { get; set; } = new SigningTarget();

        /// <summary>macOS Developer ID signing and notarization.</summary>
        public SigningTarget Macos { get; set; } = new SigningTarget();
    }
}
