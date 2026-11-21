namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The packaging channels, the operating system each must run on, and the order a host builds them in.</summary>
    public static class ChannelCatalog
    {
        /// <summary>Every channel in build order. <c>publish</c> runs before any packaging channel.</summary>
        public static IReadOnlyList<string> All { get; } = new[] { "publish", "zip", "inno", "app", "tarball", "pkg", "dmg", "deb", "rpm", "appimage" };

        /// <summary>Channels that run on any host because they only need .NET.</summary>
        public static IReadOnlyList<string> Utility { get; } = new[] { "artwork", "checksums", "notes", "verify-release", "verify-assets", "list" };

        /// <summary>Returns the operating system that owns a packaging channel's runtime identifiers.</summary>
        /// <param name="channel">A channel from <see cref="All"/>.</param>
        /// <returns><c>windows</c>, <c>macos</c>, <c>linux</c>, or <c>any</c> for publish.</returns>
        /// <exception cref="PublisherException">The channel is unknown.</exception>
        public static string OperatingSystem(string channel) => channel switch
        {
            "publish" => "any",
            "zip" or "inno" => "windows",
            "app" or "pkg" or "dmg" => "macos",
            "tarball" => "any",
            "deb" or "rpm" or "appimage" => "linux",
            _ => throw new PublisherException("Unknown channel '" + channel + "'. Valid channels: " + string.Join(", ", All.Concat(Utility)) + ".")
        };

        /// <summary>Returns the enabled channels a host operating system builds, in order.</summary>
        /// <param name="manifest">The manifest whose channel list is consulted.</param>
        /// <param name="hostOperatingSystem"><c>windows</c>, <c>macos</c>, or <c>linux</c>.</param>
        /// <returns>The channels, starting with <c>publish</c>.</returns>
        public static IReadOnlyList<string> ForHost(PublisherManifest manifest, string hostOperatingSystem)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            return All.Where(channel => channel == "publish" || (manifest.Channels.Contains(channel, StringComparer.Ordinal)
                && (OperatingSystem(channel) == hostOperatingSystem || (channel == "tarball" && hostOperatingSystem != "windows")))).ToList();
        }

        /// <summary>Returns the operating system of the current process: <c>windows</c>, <c>macos</c>, or <c>linux</c>.</summary>
        public static string Host => System.OperatingSystem.IsWindows() ? "windows" : System.OperatingSystem.IsMacOS() ? "macos" : "linux";
    }
}
