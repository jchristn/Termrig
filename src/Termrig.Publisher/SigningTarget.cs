namespace Termrig.Publisher
{
    /// <summary>Code-signing configuration for one operating system. Unsigned (ad-hoc on macOS) when not configured.</summary>
    public sealed class SigningTarget
    {
        /// <summary>True to sign with a purchased certificate. Default false: this software is not code-signed by policy.</summary>
        public bool IsConfigured { get; set; }

        /// <summary>macOS Developer ID Application identity. Ignored on Windows.</summary>
        public string Identity { get; set; } = "";

        /// <summary>macOS Developer ID Installer identity used for the .pkg. Ignored on Windows.</summary>
        public string InstallerIdentity { get; set; } = "";

        /// <summary>macOS notarytool keychain profile. Empty skips notarization.</summary>
        public string NotaryProfile { get; set; } = "";

        /// <summary>Windows: environment variable holding the path of the .pfx certificate.</summary>
        public string CertificateEnvironment { get; set; } = "";

        /// <summary>Windows: environment variable holding the certificate password.</summary>
        public string PasswordEnvironment { get; set; } = "";

        /// <summary>Windows: RFC 3161 timestamp server.</summary>
        public string TimestampUrl { get; set; } = "";
    }
}
