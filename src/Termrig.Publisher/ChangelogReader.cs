namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Extracts one version's section from a Keep a Changelog style file.</summary>
    public static class ChangelogReader
    {
        /// <summary>Returns the body of the <c>## [version]</c> section, trimmed.</summary>
        /// <param name="changelog">The changelog text.</param>
        /// <param name="version">The version whose section is wanted.</param>
        /// <returns>The section body without its heading.</returns>
        /// <exception cref="PublisherException">The section is missing or empty.</exception>
        public static string Section(string changelog, string version)
        {
            ArgumentNullException.ThrowIfNull(changelog);
            ArgumentNullException.ThrowIfNull(version);
            string heading = "## [" + version + "]";
            List<string> lines = changelog.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
            int start = lines.FindIndex(line => line.StartsWith(heading, StringComparison.Ordinal));
            if (start < 0) throw new PublisherException("CHANGELOG.md has no section starting with '" + heading + "'. Move the release's entries under that heading before tagging.");
            int end = lines.FindIndex(start + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
            string body = string.Join("\n", lines.Skip(start + 1).Take((end < 0 ? lines.Count : end) - start - 1)).Trim();
            if (body.Length == 0) throw new PublisherException("CHANGELOG.md section '" + heading + "' is empty.");
            return body;
        }
    }
}
