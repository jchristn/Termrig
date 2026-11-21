namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Resolves options into a context and runs the requested channels in order.</summary>
    public static class ChannelRunner
    {
        /// <summary>Runs the channels named by the options.</summary>
        /// <param name="options">Parsed options.</param>
        /// <param name="cancellationToken">Cancels the run.</param>
        /// <returns>A task that completes when every channel succeeds.</returns>
        /// <exception cref="PublisherException">A channel failed or a channel cannot run on this host.</exception>
        public static async Task RunAsync(PublisherOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(options);
            string root = options.Root != null ? Path.GetFullPath(options.Root) : ManifestLoader.FindRoot(Environment.CurrentDirectory);
            PublisherManifest manifest = ManifestLoader.Load(Path.Combine(root, "publisher.json"));
            string version = options.Version ?? VersionReader.Read(Path.Combine(root, manifest.Project.VersionFile));
            string host = ChannelCatalog.Host;
            IReadOnlyList<string> rids = options.Rids ?? manifest.Runtimes.Where(rid => RuntimeTarget.Parse(rid).OperatingSystem == host).ToList();
            PublisherContext context = new PublisherContext(manifest, root, version, options.Output, rids);
            List<string> channels = options.All ? ChannelCatalog.ForHost(manifest, host).Append("checksums").ToList() : options.Channels;
            foreach (string channel in channels)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine("== " + channel + " (" + context.Version + ", " + (context.Targets.Count == 0 ? "no runtimes" : string.Join(", ", context.Targets)) + ")");
                await RunChannelAsync(context, channel, options, host, cancellationToken).ConfigureAwait(false);
            }
        }

        private static async Task RunChannelAsync(PublisherContext context, string channel, PublisherOptions options, string host, CancellationToken cancellationToken)
        {
            if (ChannelCatalog.All.Contains(channel, StringComparer.Ordinal))
            {
                string owner = ChannelCatalog.OperatingSystem(channel);
                if (owner != "any" && owner != host) throw new PublisherException("Channel '" + channel + "' builds " + owner + " packages and must run on " + owner + "; this host is " + host + ". CI runs it on its own runner.");
            }
            switch (channel)
            {
                case "list": Console.WriteLine(string.Join(" ", ChannelCatalog.ForHost(context.Manifest, host))); break;
                case "artwork": ArtworkBuilder.Build(context); Console.WriteLine("Wrote " + context.ArtworkDirectory); break;
                case "publish": await PublishChannel.RunAsync(context, cancellationToken).ConfigureAwait(false); break;
                case "zip": await ArchiveChannel.ZipAsync(context, cancellationToken).ConfigureAwait(false); break;
                case "tarball": await ArchiveChannel.TarballAsync(context, cancellationToken).ConfigureAwait(false); break;
                case "inno": await WindowsChannel.InnoAsync(context, cancellationToken).ConfigureAwait(false); break;
                case "app": await MacChannel.AppAsync(context, cancellationToken).ConfigureAwait(false); break;
                case "pkg": await MacChannel.PkgAsync(context, cancellationToken).ConfigureAwait(false); break;
                case "dmg": await MacChannel.DmgAsync(context, cancellationToken).ConfigureAwait(false); break;
                case "deb": await LinuxChannel.DebAsync(context, cancellationToken).ConfigureAwait(false); break;
                case "rpm": await LinuxChannel.RpmAsync(context, cancellationToken).ConfigureAwait(false); break;
                case "appimage": await LinuxChannel.AppImageAsync(context, cancellationToken).ConfigureAwait(false); break;
                case "checksums":
                    Console.Write(await ChecksumWriter.WriteAsync(context.OutputDirectory, cancellationToken).ConfigureAwait(false));
                    break;
                case "verify-release":
                    ReleaseChannel.Verify(context, options.Tag ?? throw new PublisherException("verify-release needs --tag v<version>."));
                    ReleaseChannel.WriteGitHubOutputs(context);
                    break;
                case "verify-assets": ReleaseChannel.VerifyAssets(context); break;
                case "notes":
                    await ReleaseChannel.NotesAsync(context, options.NotesOutput ?? Path.Combine(context.WorkDirectory, "release-notes.md"), cancellationToken).ConfigureAwait(false);
                    break;
                default: ChannelCatalog.OperatingSystem(channel); break;
            }
        }
    }
}
