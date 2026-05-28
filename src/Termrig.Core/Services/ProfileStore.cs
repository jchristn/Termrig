namespace Termrig.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Termrig.Core.Models;

    /// <summary>
    /// Stores terminal profiles in JSON under the user's home directory.
    /// </summary>
    public class ProfileStore
    {
        #region Public-Members

        /// <summary>
        /// Directory containing Termrig profile data.
        /// </summary>
        public string DirectoryPath
        {
            get
            {
                return _DirectoryPath;
            }
        }

        /// <summary>
        /// Full profile JSON file path.
        /// </summary>
        public string FilePath
        {
            get
            {
                return Path.Combine(_DirectoryPath, Constants.ProfilesFilename);
            }
        }

        #endregion

        #region Private-Members

        private readonly string _DirectoryPath;
        private readonly JsonSerializerOptions _JsonOptions;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the profile store.
        /// </summary>
        /// <param name="directoryPath">Optional storage directory. Defaults to ~/.termrig.</param>
        /// <exception cref="ArgumentNullException">Thrown when the directory path is null or empty.</exception>
        public ProfileStore(string? directoryPath = null)
        {
            _DirectoryPath = ResolveDirectoryPath(directoryPath);
            _JsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            _JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Load all profiles.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Profiles from disk, or an empty list if no file exists.</returns>
        public async Task<List<TerminalProfile>> LoadAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(FilePath)) return new List<TerminalProfile>();

            using (FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                List<TerminalProfile>? profiles = await JsonSerializer.DeserializeAsync<List<TerminalProfile>>(
                    stream,
                    _JsonOptions,
                    token).ConfigureAwait(false);
                return profiles ?? new List<TerminalProfile>();
            }
        }

        /// <summary>
        /// Save all profiles.
        /// </summary>
        /// <param name="profiles">Profiles to write.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when profiles is null.</exception>
        public async Task SaveAsync(List<TerminalProfile> profiles, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(profiles);
            token.ThrowIfCancellationRequested();

            Directory.CreateDirectory(_DirectoryPath);
            using (FileStream stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, profiles, _JsonOptions, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Upsert a profile by identifier.
        /// </summary>
        /// <param name="profile">Profile to insert or replace.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when profile is null.</exception>
        public async Task UpsertAsync(TerminalProfile profile, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(profile);
            List<TerminalProfile> profiles = await LoadAsync(token).ConfigureAwait(false);
            TerminalProfile? existing = profiles.FirstOrDefault(item => item.Id == profile.Id);
            if (existing != null)
            {
                Int32 index = profiles.IndexOf(existing);
                profiles[index] = profile;
            }
            else
            {
                profiles.Add(profile);
            }

            await SaveAsync(profiles, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a profile by identifier.
        /// </summary>
        /// <param name="profileId">Profile identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a profile was removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when profileId is null or empty.</exception>
        public async Task<bool> DeleteAsync(string profileId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(profileId)) throw new ArgumentNullException(nameof(profileId));
            List<TerminalProfile> profiles = await LoadAsync(token).ConfigureAwait(false);
            Int32 removed = profiles.RemoveAll(item => item.Id == profileId);
            await SaveAsync(profiles, token).ConfigureAwait(false);
            return removed > 0;
        }

        #endregion

        #region Private-Methods

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
