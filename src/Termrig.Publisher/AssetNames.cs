namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Release file names. Every name carries the version; Debian and RPM follow their own conventions.</summary>
    public static class AssetNames
    {
        /// <summary>Returns the file name a channel produces for one runtime identifier.</summary>
        /// <param name="command">Lowercase package name, for example <c>termrig</c>.</param>
        /// <param name="version">Release version.</param>
        /// <param name="channel">Packaging channel.</param>
        /// <param name="target">Runtime identifier being packaged.</param>
        /// <returns>The file name, without a directory.</returns>
        /// <exception cref="PublisherException">The channel produces no release file.</exception>
        public static string For(string command, string version, string channel, RuntimeTarget target)
        {
            ArgumentNullException.ThrowIfNull(target);
            return channel switch
            {
                "zip" => command + "-" + version + "-" + target.Rid + ".zip",
                "inno" => command + "-" + version + "-" + target.Rid + "-setup.exe",
                "tarball" => command + "-" + version + "-" + target.Rid + ".tar.gz",
                "pkg" => command + "-" + version + "-" + target.Rid + ".pkg",
                "dmg" => command + "-" + version + "-" + target.Rid + ".dmg",
                "deb" => command + "_" + version + "_" + target.DebianArchitecture + ".deb",
                "rpm" => command + "-" + RpmVersion(version) + "-1." + target.GnuArchitecture + ".rpm",
                "appimage" => command + "-" + version + "-" + target.GnuArchitecture + ".AppImage",
                _ => throw new PublisherException("Channel '" + channel + "' does not produce a release file.")
            };
        }

        /// <summary>Every file a complete release holds: each enabled channel for each manifest runtime, plus SHA256SUMS.</summary>
        /// <param name="manifest">The manifest.</param>
        /// <param name="version">Release version.</param>
        /// <returns>The expected file names, sorted ordinally.</returns>
        public static IReadOnlyList<string> Expected(PublisherManifest manifest, string version)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            List<string> names = new List<string> { ChecksumWriter.FileName };
            foreach (string channel in manifest.Channels.Where(channel => channel != "app"))
            {
                foreach (RuntimeTarget target in manifest.Runtimes.Select(RuntimeTarget.Parse))
                {
                    bool applies = channel == "tarball" ? target.OperatingSystem != "windows" : ChannelCatalog.OperatingSystem(channel) == target.OperatingSystem;
                    if (applies) names.Add(For(manifest.Project.Command, version, channel, target));
                }
            }
            return names.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToList();
        }

        /// <summary>RPM versions cannot contain hyphens; a pre-release suffix uses a tilde so it sorts before the release.</summary>
        /// <param name="version">Semantic version.</param>
        /// <returns>The RPM-safe version.</returns>
        public static string RpmVersion(string version)
        {
            ArgumentNullException.ThrowIfNull(version);
            return version.Replace('-', '~');
        }

        /// <summary>Debian versions sort a tilde before the release, so a pre-release suffix uses one.</summary>
        /// <param name="version">Semantic version.</param>
        /// <returns>The Debian-safe version.</returns>
        public static string DebianVersion(string version) => RpmVersion(version);
    }
}
