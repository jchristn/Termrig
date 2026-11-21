namespace Termrig.Publisher
{
    using System;

    /// <summary>A .NET runtime identifier and the architecture names each packaging format expects for it.</summary>
    public sealed class RuntimeTarget
    {
        /// <summary>The runtime identifier, for example <c>linux-arm64</c>.</summary>
        public string Rid { get; }

        /// <summary>Operating system: <c>windows</c>, <c>macos</c>, or <c>linux</c>.</summary>
        public string OperatingSystem { get; }

        /// <summary>True for the 64-bit ARM architecture; false for x64.</summary>
        public bool IsArm64 { get; }

        /// <summary>Debian architecture: <c>amd64</c> or <c>arm64</c>.</summary>
        public string DebianArchitecture => IsArm64 ? "arm64" : "amd64";

        /// <summary>RPM and AppImage architecture: <c>x86_64</c> or <c>aarch64</c>.</summary>
        public string GnuArchitecture => IsArm64 ? "aarch64" : "x86_64";

        /// <summary>Inno Setup architecture identifier: <c>x64compatible</c> or <c>arm64</c>.</summary>
        public string InnoArchitecture => IsArm64 ? "arm64" : "x64compatible";

        /// <summary>macOS installer host architecture: <c>x86_64</c> or <c>arm64</c>.</summary>
        public string MacArchitecture => IsArm64 ? "arm64" : "x86_64";

        /// <summary>File name suffix for executables on this operating system.</summary>
        public string ExecutableSuffix => OperatingSystem == "windows" ? ".exe" : "";

        private RuntimeTarget(string rid, string operatingSystem, bool isArm64)
        {
            Rid = rid;
            OperatingSystem = operatingSystem;
            IsArm64 = isArm64;
        }

        /// <summary>Parses one of the six supported runtime identifiers.</summary>
        /// <param name="rid">A runtime identifier such as <c>win-x64</c>, <c>osx-arm64</c>, or <c>linux-x64</c>.</param>
        /// <returns>The parsed target.</returns>
        /// <exception cref="PublisherException">The identifier is not one of the supported six.</exception>
        public static RuntimeTarget Parse(string rid)
        {
            ArgumentNullException.ThrowIfNull(rid);
            string[] parts = rid.Split('-');
            if (parts.Length != 2 || (parts[1] != "x64" && parts[1] != "arm64"))
                throw new PublisherException("Unsupported runtime identifier '" + rid + "'. Use win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, or linux-arm64.");
            string os = parts[0] switch
            {
                "win" => "windows",
                "osx" => "macos",
                "linux" => "linux",
                _ => throw new PublisherException("Unsupported runtime identifier '" + rid + "'. Use win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, or linux-arm64.")
            };
            return new RuntimeTarget(rid, os, parts[1] == "arm64");
        }

        /// <inheritdoc />
        public override string ToString() => Rid;
    }
}
