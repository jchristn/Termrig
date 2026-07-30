namespace Termrig.Core.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Security.Cryptography;

    /// <summary>
    /// Records configuration files written by this process so a <see cref="FileSystemWatcher"/> can
    /// distinguish external edits from echoes of the app's own writes. Suppressing self-write echoes
    /// breaks the write -> watcher -> full-reload feedback loop that otherwise fires on routine
    /// workspace-side saves (directory capture, font zoom, tab reordering, etc.).
    /// </summary>
    public static class ConfigWriteRegistry
    {
        private static readonly ConcurrentDictionary<string, string> _LastWriteHashes =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Records the content this process just wrote (or is about to write) to <paramref name="path"/>.
        /// Call before the write so a watcher event that arrives immediately after is recognized.
        /// </summary>
        public static void RecordWrite(string path, byte[] content)
        {
            if (String.IsNullOrWhiteSpace(path) || content == null) return;
            _LastWriteHashes[Normalize(path)] = Hash(content);
        }

        /// <summary>
        /// Returns true when <paramref name="currentContent"/> is byte-for-byte identical to the most
        /// recent content this process recorded for <paramref name="path"/>.
        /// </summary>
        public static bool IsEchoOfLastWrite(string path, byte[] currentContent)
        {
            if (String.IsNullOrWhiteSpace(path) || currentContent == null) return false;
            return _LastWriteHashes.TryGetValue(Normalize(path), out string? hash) &&
                   String.Equals(hash, Hash(currentContent), StringComparison.Ordinal);
        }

        /// <summary>
        /// Reads <paramref name="path"/> and reports whether its current on-disk content matches the
        /// last write this process recorded. Returns false if the file cannot be read.
        /// </summary>
        public static bool IsEchoOfLastWrite(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;

            try
            {
                if (!File.Exists(path)) return false;
                return IsEchoOfLastWrite(path, File.ReadAllBytes(path));
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>
        /// Clears all recorded writes. Intended for test isolation.
        /// </summary>
        public static void Clear()
        {
            _LastWriteHashes.Clear();
        }

        private static string Normalize(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception)
            {
                return path;
            }
        }

        private static string Hash(byte[] content)
        {
            return Convert.ToHexString(SHA256.HashData(content));
        }
    }
}
