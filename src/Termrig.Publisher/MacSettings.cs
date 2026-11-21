namespace Termrig.Publisher
{
    /// <summary>macOS bundle metadata.</summary>
    public sealed class MacSettings
    {
        /// <summary>Minimum macOS version for the bundle and installer.</summary>
        public string MinimumSystemVersion { get; set; } = "12.0";

        /// <summary>Directory that receives the PATH symlink. Created by the installer when missing.</summary>
        public string CommandDirectory { get; set; } = "/usr/local/bin";
    }
}
