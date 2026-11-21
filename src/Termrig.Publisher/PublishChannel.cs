namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Builds every artifact as a self-contained single-file executable per runtime identifier.</summary>
    public static class PublishChannel
    {
        /// <summary>Returns the <c>dotnet publish</c> arguments for one artifact and runtime identifier.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="artifact">The artifact to publish.</param>
        /// <param name="target">The runtime identifier.</param>
        /// <param name="output">The publish output directory.</param>
        /// <returns>The argument list, without the dotnet executable.</returns>
        public static IReadOnlyList<string> Arguments(PublisherContext context, ArtifactInfo artifact, RuntimeTarget target, string output)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(artifact);
            ArgumentNullException.ThrowIfNull(target);
            List<string> arguments = new List<string>
            {
                "publish", context.Resolve(artifact.Project),
                "-c", "Release",
                "-f", context.Manifest.Framework,
                "-r", target.Rid,
                "--self-contained", "true",
                "-p:PublishSingleFile=true",
                "-p:IncludeNativeLibrariesForSelfExtract=true",
                "-p:DebugType=none",
                "-p:PublishDocumentationFiles=false",
                "-p:PublishReferencesDocumentationFiles=false",
                "-p:Version=" + context.Version,
                "-o", output
            };
            foreach (KeyValuePair<string, string> property in artifact.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                arguments.Add("-p:" + property.Key + "=" + property.Value);
            if (target.OperatingSystem == "windows") arguments.Add("-p:ApplicationIcon=" + Path.Combine(context.ArtworkDirectory, ArtworkBuilder.IcoFile));
            return arguments;
        }

        /// <summary>Publishes every artifact for the context's targets into their payload directories.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="cancellationToken">Cancels the build.</param>
        /// <returns>A task that completes when every payload is ready.</returns>
        /// <exception cref="PublisherException">A build failed or produced no executable.</exception>
        public static async Task RunAsync(PublisherContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArtworkBuilder.Build(context);
            string dotnet = ToolRunner.Require("dotnet");
            foreach (RuntimeTarget target in context.Targets)
            {
                string payload = FileStaging.Fresh(context.PayloadDirectory(target));
                foreach (ArtifactInfo artifact in context.Manifest.Artifacts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string output = FileStaging.Fresh(Path.Combine(context.WorkDirectory, "publish", target.Rid, artifact.Name));
                    await ToolRunner.RunAsync(dotnet, Arguments(context, artifact, target, output), context.Root, null, cancellationToken).ConfigureAwait(false);
                    string executable = artifact.Executable + target.ExecutableSuffix;
                    if (!File.Exists(Path.Combine(output, executable))) throw new PublisherException("dotnet publish produced no " + executable + " in " + output + ".");
                    foreach (string file in Directory.GetFiles(output, "*", SearchOption.AllDirectories).Where(path => !path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                    {
                        string name = Path.GetRelativePath(output, file);
                        if (name != executable) Console.WriteLine("Note: " + artifact.Name + " for " + target.Rid + " also published " + name.Replace(Path.DirectorySeparatorChar, '/') + "; it ships beside the executable.");
                        FileStaging.Copy(file, Path.Combine(payload, name), executable: name == executable);
                    }
                }
                await SignPayloadAsync(context, target, payload, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>Signs the payload executables: ad-hoc (or Developer ID) on macOS, Authenticode on Windows when configured.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="target">The runtime identifier.</param>
        /// <param name="payload">The payload directory.</param>
        /// <param name="cancellationToken">Cancels signing.</param>
        /// <returns>A task that completes when signing is done.</returns>
        public static async Task SignPayloadAsync(PublisherContext context, RuntimeTarget target, string payload, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(target);
            if (target.OperatingSystem == "macos")
            {
                if (!OperatingSystem.IsMacOS())
                {
                    Console.WriteLine("Warning: " + target.Rid + " executables are unsigned. Apple Silicon refuses unsigned binaries; package macOS on a Mac.");
                    return;
                }
                foreach (ArtifactInfo artifact in context.Manifest.Artifacts)
                    await MacChannel.CodesignAsync(context, Path.Combine(payload, artifact.Executable), deep: false, cancellationToken).ConfigureAwait(false);
            }
            else if (target.OperatingSystem == "windows" && context.Manifest.Signing.Windows.IsConfigured)
            {
                foreach (ArtifactInfo artifact in context.Manifest.Artifacts)
                    await WindowsChannel.SignAsync(context, Path.Combine(payload, artifact.Executable + ".exe"), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
