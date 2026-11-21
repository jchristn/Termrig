namespace Termrig.Publisher
{
    using System;

    /// <summary>Composes the GitHub release body: install instructions first, then the changelog.</summary>
    public static class ReleaseNotesWriter
    {
        /// <summary>Builds the release notes.</summary>
        /// <param name="template">The install-notes template with <c>{version}</c> placeholders.</param>
        /// <param name="version">The release version.</param>
        /// <param name="changes">The changelog section for the version.</param>
        /// <returns>The Markdown release body.</returns>
        /// <exception cref="PublisherException">A placeholder other than <c>{version}</c> remains.</exception>
        public static string Compose(string template, string version, string changes)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(version);
            ArgumentNullException.ThrowIfNull(changes);
            string install = template.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("{version}", version, StringComparison.Ordinal).Trim();
            return install + "\n\n## What's changed\n\n" + changes.Trim() + "\n";
        }

        /// <summary>Returns the release title, for example <c>Termrig 0.1.0</c>.</summary>
        /// <param name="name">Product name.</param>
        /// <param name="version">Release version.</param>
        /// <returns>The title.</returns>
        public static string Title(string name, string version) => name + " " + version;
    }
}
