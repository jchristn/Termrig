namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Release-job steps: tag and changelog checks, release notes, and checksums.</summary>
    public static class ReleaseChannel
    {
        /// <summary>Fails unless the tag is <c>v</c> plus the project version and the changelog has a non-empty section for it.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="tag">The pushed tag, for example <c>v0.1.0</c>.</param>
        /// <exception cref="PublisherException">The tag does not match or the changelog section is missing.</exception>
        public static void Verify(PublisherContext context, string tag)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(tag);
            string expected = "v" + context.Version;
            if (!string.Equals(tag, expected, StringComparison.Ordinal))
                throw new PublisherException("Tag '" + tag + "' does not match the project version; expected '" + expected + "'. Fix the version file or re-tag.");
            ChangelogReader.Section(File.ReadAllText(context.Resolve(context.Manifest.Project.Changelog)), context.Version);
            if (!File.Exists(context.Resolve(context.Manifest.Project.InstallNotes))) throw new PublisherException("Release notes template not found: " + context.Manifest.Project.InstallNotes + ".");
        }

        /// <summary>Fails unless the output directory holds exactly the files a complete release needs.</summary>
        /// <param name="context">The publisher context.</param>
        /// <returns>The verified file names.</returns>
        /// <exception cref="PublisherException">A file is missing or unexpected; the message lists each one.</exception>
        public static IReadOnlyList<string> VerifyAssets(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            IReadOnlyList<string> expected = AssetNames.Expected(context.Manifest, context.Version);
            List<string> actual = Directory.Exists(context.OutputDirectory)
                ? Directory.GetFiles(context.OutputDirectory).Select(Path.GetFileName).OfType<string>().Where(name => !name.StartsWith('.')).ToList()
                : new List<string>();
            List<string> problems = expected.Except(actual, StringComparer.Ordinal).Select(name => "MISSING: " + name)
                .Concat(actual.Except(expected, StringComparer.Ordinal).Select(name => "UNEXPECTED: " + name)).ToList();
            if (problems.Count > 0) throw new PublisherException("The release is incomplete (" + actual.Count + " of " + expected.Count + " files):" + Environment.NewLine + string.Join(Environment.NewLine, problems));
            Console.WriteLine("All " + expected.Count + " release files are present.");
            return expected;
        }

        /// <summary>Writes the release notes file.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="output">Destination path.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        /// <returns>A task that completes when the file is written.</returns>
        public static async Task NotesAsync(PublisherContext context, string output, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(output);
            string template = await File.ReadAllTextAsync(context.Resolve(context.Manifest.Project.InstallNotes), cancellationToken).ConfigureAwait(false);
            string changelog = await File.ReadAllTextAsync(context.Resolve(context.Manifest.Project.Changelog), cancellationToken).ConfigureAwait(false);
            string notes = ReleaseNotesWriter.Compose(template, context.Version, ChangelogReader.Section(changelog, context.Version));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            await File.WriteAllTextAsync(output, notes, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            Console.WriteLine("Wrote " + output);
        }

        /// <summary>Appends the release name, version, and pre-release flag to the GitHub Actions output file when one is set.</summary>
        /// <param name="context">The publisher context.</param>
        public static void WriteGitHubOutputs(PublisherContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            string prerelease = VersionReader.IsPrerelease(context.Version, context.Manifest.PrereleaseBeforeOne) ? "true" : "false";
            string lines = "version=" + context.Version + "\n" + "name=" + ReleaseNotesWriter.Title(context.Manifest.Project.Name, context.Version) + "\n" + "prerelease=" + prerelease + "\n";
            Console.Write(lines);
            string? path = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
            if (!string.IsNullOrWhiteSpace(path)) File.AppendAllText(path, lines);
        }
    }
}
