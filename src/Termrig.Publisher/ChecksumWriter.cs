namespace Termrig.Publisher
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Writes one SHA256SUMS file covering every release asset, in the format <c>sha256sum -c</c> reads.</summary>
    public static class ChecksumWriter
    {
        /// <summary>The checksum file name.</summary>
        public const string FileName = "SHA256SUMS";

        /// <summary>Hashes every file in a directory (not subdirectories) and writes SHA256SUMS beside them.</summary>
        /// <param name="directory">The release output directory.</param>
        /// <param name="cancellationToken">Cancels hashing.</param>
        /// <returns>The checksum file text.</returns>
        /// <exception cref="PublisherException">The directory holds no assets.</exception>
        public static async Task<string> WriteAsync(string directory, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            string[] files = Directory.Exists(directory)
                ? Directory.GetFiles(directory).Where(path => Path.GetFileName(path) != FileName && !Path.GetFileName(path).StartsWith('.')).OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            if (files.Length == 0) throw new PublisherException("No release files in " + directory + " to checksum.");
            StringBuilder sums = new StringBuilder();
            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using FileStream stream = File.OpenRead(file);
                byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
                sums.Append(Convert.ToHexStringLower(hash)).Append("  ").Append(Path.GetFileName(file)).Append('\n');
            }
            string text = sums.ToString();
            await File.WriteAllTextAsync(Path.Combine(directory, FileName), text, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            return text;
        }
    }
}
