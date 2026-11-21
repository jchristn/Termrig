namespace Termrig.Publisher
{
    using System;
    using System.Formats.Tar;
    using System.IO;
    using System.IO.Compression;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Builds the portable .zip (Windows) and .tar.gz (macOS, Linux) archives holding the bare executables.</summary>
    public static class ArchiveChannel
    {
        /// <summary>Writes one .zip per Windows target.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="cancellationToken">Cancels archiving.</param>
        /// <returns>A task that completes when every archive is written.</returns>
        public static Task ZipAsync(PublisherContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            Directory.CreateDirectory(context.OutputDirectory);
            foreach (RuntimeTarget target in context.TargetsFor("windows"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string output = context.OutputFile("zip", target);
                if (File.Exists(output)) File.Delete(output);
                using ZipArchive archive = ZipFile.Open(output, ZipArchiveMode.Create);
                foreach (ArtifactInfo artifact in context.Manifest.Artifacts)
                {
                    string name = artifact.Executable + target.ExecutableSuffix;
                    string source = Path.Combine(context.PayloadDirectory(target), name);
                    if (!File.Exists(source)) throw new PublisherException("Missing " + source + ". Run the publish channel for " + target.Rid + " first.");
                    archive.CreateEntryFromFile(source, name, CompressionLevel.SmallestSize);
                }
                foreach (string extra in context.PayloadExtras(target))
                    archive.CreateEntryFromFile(Path.Combine(context.PayloadDirectory(target), extra), extra, CompressionLevel.SmallestSize);
                if (context.PathCommandIsAlias)
                {
                    ZipArchiveEntry shim = archive.CreateEntry(InnoScriptWriter.ShimFile(context), CompressionLevel.SmallestSize);
                    using (StreamWriter writer = new StreamWriter(shim.Open())) writer.Write(InnoScriptWriter.CommandShim(context, target));
                }
                ZipArchiveEntry license = archive.CreateEntry("LICENSE.txt", CompressionLevel.SmallestSize);
                using (StreamWriter writer = new StreamWriter(license.Open())) writer.Write(context.LicenseText().Replace("\n", "\r\n", StringComparison.Ordinal));
                Console.WriteLine("Wrote " + output);
            }
            return Task.CompletedTask;
        }

        /// <summary>Writes one .tar.gz per macOS (on a Mac) and Linux target, preserving executable modes.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="cancellationToken">Cancels archiving.</param>
        /// <returns>A task that completes when every archive is written.</returns>
        public static async Task TarballAsync(PublisherContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            Directory.CreateDirectory(context.OutputDirectory);
            foreach (RuntimeTarget target in context.Targets)
            {
                if (target.OperatingSystem == "windows") continue;
                string output = context.OutputFile("tarball", target);
                await using (FileStream file = File.Create(output))
                await using (GZipStream gzip = new GZipStream(file, CompressionLevel.SmallestSize))
                await using (TarWriter tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
                {
                    foreach (ArtifactInfo artifact in context.Manifest.Artifacts)
                    {
                        string source = Path.Combine(context.PayloadDirectory(target), artifact.Executable);
                        if (!File.Exists(source)) throw new PublisherException("Missing " + source + ". Run the publish channel for " + target.Rid + " first.");
                        await AddAsync(tar, artifact.Executable, File.ReadAllBytes(source), executable: true, cancellationToken).ConfigureAwait(false);
                    }
                    foreach (string extra in context.PayloadExtras(target))
                        await AddAsync(tar, extra, File.ReadAllBytes(Path.Combine(context.PayloadDirectory(target), extra)), executable: false, cancellationToken).ConfigureAwait(false);
                    await AddAsync(tar, "LICENSE.md", System.Text.Encoding.UTF8.GetBytes(context.LicenseText()), executable: false, cancellationToken).ConfigureAwait(false);
                }
                Console.WriteLine("Wrote " + output);
            }
        }

        private static async Task AddAsync(TarWriter tar, string name, byte[] content, bool executable, CancellationToken cancellationToken)
        {
            PaxTarEntry entry = new PaxTarEntry(TarEntryType.RegularFile, name)
            {
                Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead
                    | (executable ? UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute : UnixFileMode.None),
                ModificationTime = DateTimeOffset.UtcNow,
                DataStream = new MemoryStream(content)
            };
            await tar.WriteEntryAsync(entry, cancellationToken).ConfigureAwait(false);
        }
    }
}
