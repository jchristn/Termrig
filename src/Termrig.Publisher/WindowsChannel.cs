namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Builds the Inno Setup installer on Windows and applies Authenticode signing when configured.</summary>
    public static class WindowsChannel
    {
        /// <summary>Compiles one setup executable per Windows target.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="cancellationToken">Cancels the build.</param>
        /// <returns>A task that completes when every installer is written.</returns>
        /// <exception cref="PublisherException">Inno Setup is missing or the compile failed.</exception>
        public static async Task InnoAsync(PublisherContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            string iscc = ToolRunner.Require("iscc");
            if (!File.Exists(Path.Combine(context.ArtworkDirectory, ArtworkBuilder.IcoFile))) ArtworkBuilder.Build(context);
            Directory.CreateDirectory(context.OutputDirectory);
            foreach (RuntimeTarget target in context.TargetsFor("windows"))
            {
                string work = FileStaging.Fresh(Path.Combine(context.WorkDirectory, "windows", target.Rid));
                string license = Path.Combine(work, "LICENSE.txt");
                File.WriteAllText(license, context.LicenseText().Replace("\n", "\r\n", StringComparison.Ordinal));
                if (context.PathCommandIsAlias) File.WriteAllText(Path.Combine(work, InnoScriptWriter.ShimFile(context)), InnoScriptWriter.CommandShim(context, target));
                string script = Path.Combine(work, context.Manifest.Project.Command + ".iss");
                File.WriteAllText(script, InnoScriptWriter.Build(context, target, context.PayloadDirectory(target), license), new System.Text.UTF8Encoding(true));
                await ToolRunner.RunAsync(iscc, new[] { "/Qp", script }, work, null, cancellationToken).ConfigureAwait(false);
                string output = context.OutputFile("inno", target);
                if (!File.Exists(output)) throw new PublisherException("Inno Setup did not produce " + output + ".");
                if (context.Manifest.Signing.Windows.IsConfigured) await SignAsync(context, output, cancellationToken).ConfigureAwait(false);
                Console.WriteLine("Wrote " + output);
            }
        }

        /// <summary>Signs a file with signtool using the certificate named by the manifest's environment variables.</summary>
        /// <param name="context">The publisher context.</param>
        /// <param name="file">The executable to sign.</param>
        /// <param name="cancellationToken">Cancels signing.</param>
        /// <returns>A task that completes when the file is signed.</returns>
        /// <exception cref="PublisherException">The certificate variables are unset or signtool failed.</exception>
        public static async Task SignAsync(PublisherContext context, string file, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            SigningTarget signing = context.Manifest.Signing.Windows;
            string? certificate = Environment.GetEnvironmentVariable(signing.CertificateEnvironment);
            string? password = Environment.GetEnvironmentVariable(signing.PasswordEnvironment);
            if (string.IsNullOrWhiteSpace(certificate) || string.IsNullOrEmpty(password))
                throw new PublisherException("Windows signing is configured but " + signing.CertificateEnvironment + " or " + signing.PasswordEnvironment + " is not set.");
            List<string> arguments = new List<string> { "sign", "/fd", "sha256", "/f", certificate, "/p", password };
            if (!string.IsNullOrWhiteSpace(signing.TimestampUrl)) arguments.AddRange(new[] { "/tr", signing.TimestampUrl, "/td", "sha256" });
            arguments.Add(file);
            await ToolRunner.RunToolAsync("signtool", arguments, cancellationToken).ConfigureAwait(false);
        }
    }
}
