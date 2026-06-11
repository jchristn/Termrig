namespace Termrig.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Termrig.Core.Models;

    /// <summary>
    /// Stores profile folder metadata in JSON under the user's home directory.
    /// </summary>
    public class ProfileFolderStore
    {
        #region Public-Members

        /// <summary>
        /// Directory containing Termrig profile folder data.
        /// </summary>
        public string DirectoryPath
        {
            get
            {
                return _DirectoryPath;
            }
        }

        /// <summary>
        /// Full profile folder JSON file path.
        /// </summary>
        public string FilePath
        {
            get
            {
                return Path.Combine(_DirectoryPath, Constants.ProfileFoldersFilename);
            }
        }

        #endregion

        #region Private-Members

        private readonly string _DirectoryPath;
        private readonly JsonSerializerOptions _JsonOptions;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the profile folder store.
        /// </summary>
        /// <param name="directoryPath">Optional storage directory. Defaults to ~/.termrig.</param>
        public ProfileFolderStore(string? directoryPath = null)
        {
            _DirectoryPath = ResolveDirectoryPath(directoryPath);
            _JsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Load all profile folders.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Profile folders, or an empty list if no file exists.</returns>
        public async Task<List<ProfileFolder>> LoadAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(FilePath)) return new List<ProfileFolder>();

            using (FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                List<ProfileFolder>? folders = await JsonSerializer.DeserializeAsync<List<ProfileFolder>>(
                    stream,
                    _JsonOptions,
                    token).ConfigureAwait(false);
                return Normalize(folders);
            }
        }

        /// <summary>
        /// Save all profile folders.
        /// </summary>
        /// <param name="folders">Folders to write.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when folders is null.</exception>
        public async Task SaveAsync(List<ProfileFolder> folders, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(folders);
            token.ThrowIfCancellationRequested();

            List<ProfileFolder> normalized = Normalize(folders);
            Directory.CreateDirectory(_DirectoryPath);
            using (FileStream stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, _JsonOptions, token).ConfigureAwait(false);
            }
        }

        #endregion

        #region Private-Methods

        private static List<ProfileFolder> Normalize(List<ProfileFolder>? folders)
        {
            if (folders == null) return new List<ProfileFolder>();

            return folders
                .Where(item =>
                    item != null &&
                    !String.IsNullOrWhiteSpace(item.Id) &&
                    !String.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Last())
                .ToList();
        }

        private static string ResolveDirectoryPath(string? directoryPath)
        {
            if (!String.IsNullOrWhiteSpace(directoryPath)) return directoryPath;

            string? home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (String.IsNullOrWhiteSpace(home)) throw new InvalidOperationException("Unable to resolve the current user's home directory.");
            return Path.Combine(home, Constants.ApplicationDirectoryName);
        }

        #endregion
    }
}
