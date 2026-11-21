namespace Termrig.Publisher
{
    using System;
    using System.IO;

    /// <summary>File-system helpers for staging directories.</summary>
    public static class FileStaging
    {
        /// <summary>Deletes a directory if present and creates it empty.</summary>
        /// <param name="path">The directory.</param>
        /// <returns>The same path.</returns>
        public static string Fresh(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>Writes UTF-8 text without a byte-order mark, creating the directory, and optionally marks it executable.</summary>
        /// <param name="path">Destination file.</param>
        /// <param name="text">Content.</param>
        /// <param name="executable">True for mode 0755 on Unix; otherwise 0644.</param>
        public static void WriteText(string path, string text, bool executable = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            ArgumentNullException.ThrowIfNull(text);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
            SetMode(path, executable);
        }

        /// <summary>Copies a file, creating the destination directory, and sets its Unix mode.</summary>
        /// <param name="source">Source file.</param>
        /// <param name="destination">Destination file.</param>
        /// <param name="executable">True for mode 0755 on Unix; otherwise 0644.</param>
        /// <exception cref="PublisherException">The source does not exist.</exception>
        public static void Copy(string source, string destination, bool executable)
        {
            if (!File.Exists(source)) throw new PublisherException("Expected file is missing: " + source + ". Run the publish channel first.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
            SetMode(destination, executable);
        }

        /// <summary>Sets mode 0755 or 0644 on Unix. Does nothing on Windows.</summary>
        /// <param name="path">The file.</param>
        /// <param name="executable">True for 0755.</param>
        public static void SetMode(string path, bool executable)
        {
            if (OperatingSystem.IsWindows()) return;
            UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
            if (executable) mode |= UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            File.SetUnixFileMode(path, mode);
        }

        /// <summary>Total size in bytes of every file under a directory.</summary>
        /// <param name="path">The directory.</param>
        /// <returns>The byte count.</returns>
        public static long Size(string path)
        {
            // Skip links so the disk image's Applications shortcut is not followed into /Applications.
            EnumerationOptions options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = true };
            long total = 0;
            foreach (string file in Directory.EnumerateFiles(path, "*", options)) total += new FileInfo(file).Length;
            return total;
        }
    }
}
