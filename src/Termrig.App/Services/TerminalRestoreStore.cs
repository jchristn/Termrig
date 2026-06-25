namespace Termrig.App.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Termrig.App.Models;
    using Termrig.Core;
    using Termrig.Core.Models;
    using XTerm.Restore;

    /// <summary>
    /// Persists per-profile, per-tab terminal scrollback restore snapshots.
    /// </summary>
    public sealed class TerminalRestoreStore
    {
        #region Public-Members

        /// <summary>
        /// Directory containing terminal restore snapshots.
        /// </summary>
        public string DirectoryPath
        {
            get
            {
                return _DirectoryPath;
            }
        }

        #endregion

        #region Private-Members

        private readonly string _DirectoryPath;
        private readonly JsonSerializerOptions _JsonOptions;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the terminal restore store.
        /// </summary>
        /// <param name="directoryPath">Optional storage directory. Defaults to ~/.termrig/terminal-restore.</param>
        public TerminalRestoreStore(string? directoryPath = null)
        {
            _DirectoryPath = ResolveDirectoryPath(directoryPath);
            _JsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false
            };
            _JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Load the saved buffer snapshot for a profile tab.
        /// </summary>
        public async Task<TerminalBufferSnapshot?> LoadAsync(TerminalProfile profile, TerminalTabProfile tab, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(tab);
            token.ThrowIfCancellationRequested();

            string path = GetSnapshotPath(profile, tab);
            if (!File.Exists(path)) return null;

            try
            {
                await using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                TerminalRestoreSnapshot? snapshot = await JsonSerializer.DeserializeAsync<TerminalRestoreSnapshot>(
                    stream,
                    _JsonOptions,
                    token).ConfigureAwait(false);

                if (snapshot == null || snapshot.SchemaVersion != 1 || snapshot.Buffer == null)
                    return null;

                return snapshot.Buffer;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Save the buffer snapshot for a profile tab.
        /// </summary>
        public async Task SaveAsync(
            TerminalProfile profile,
            TerminalTabProfile tab,
            TerminalBufferSnapshot buffer,
            int scrollbackLineLimit,
            string? workingDirectory = null,
            CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(tab);
            ArgumentNullException.ThrowIfNull(buffer);
            token.ThrowIfCancellationRequested();

            string path = GetSnapshotPath(profile, tab);
            string directory = Path.GetDirectoryName(path) ?? _DirectoryPath;
            Directory.CreateDirectory(directory);

            DateTimeOffset capturedAt = DateTimeOffset.UtcNow;
            buffer.CapturedAtUtc = capturedAt;
            var snapshot = new TerminalRestoreSnapshot
            {
                CapturedAtUtc = capturedAt,
                ProfileId = profile.Id,
                ProfileName = profile.Name,
                TabId = tab.Id,
                TabName = tab.Name,
                WorkingDirectory = workingDirectory ?? tab.StartingDirectory ?? String.Empty,
                ScrollbackLineLimit = scrollbackLineLimit,
                Buffer = buffer
            };

            string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(stream, snapshot, _JsonOptions, token).ConfigureAwait(false);
                }

                File.Move(tempPath, path, true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        /// <summary>
        /// Delete the saved snapshot for a profile tab.
        /// </summary>
        public Task DeleteAsync(TerminalProfile profile, TerminalTabProfile tab, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(tab);
            token.ThrowIfCancellationRequested();

            DeleteFileIfExists(GetSnapshotPath(profile, tab));
            RemoveDirectoryIfEmpty(GetProfileDirectory(profile));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Delete all saved snapshots for a profile.
        /// </summary>
        public Task DeleteProfileAsync(TerminalProfile profile, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(profile);
            token.ThrowIfCancellationRequested();

            string directory = GetProfileDirectory(profile);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }

            return Task.CompletedTask;
        }

        #endregion

        #region Private-Methods

        private static string ResolveDirectoryPath(string? directoryPath)
        {
            if (!String.IsNullOrWhiteSpace(directoryPath)) return directoryPath;

            string? home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (String.IsNullOrWhiteSpace(home)) throw new InvalidOperationException("Unable to resolve the current user's home directory.");
            return Path.Combine(home, Constants.ApplicationDirectoryName, Constants.TerminalRestoreSubdirectoryName);
        }

        private string GetSnapshotPath(TerminalProfile profile, TerminalTabProfile tab)
        {
            return Path.Combine(GetProfileDirectory(profile), SanitizePathComponent(tab.Id) + ".json");
        }

        private string GetProfileDirectory(TerminalProfile profile)
        {
            return Path.Combine(_DirectoryPath, SanitizePathComponent(profile.Id));
        }

        private static string SanitizePathComponent(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return Guid.NewGuid().ToString("N");

            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
            return String.IsNullOrWhiteSpace(sanitized) ? Guid.NewGuid().ToString("N") : sanitized;
        }

        private static void DeleteFileIfExists(string path)
        {
            if (!File.Exists(path)) return;
            File.Delete(path);
        }

        private static void RemoveDirectoryIfEmpty(string directory)
        {
            if (!Directory.Exists(directory)) return;
            if (Directory.EnumerateFileSystemEntries(directory).Any()) return;
            Directory.Delete(directory);
        }

        #endregion
    }
}
