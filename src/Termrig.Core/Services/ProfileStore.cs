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

        private static readonly SemaphoreSlim _FileAccessLock = new SemaphoreSlim(1, 1);
        private const int FileAccessRetryCount = 5;
        private static readonly TimeSpan FileAccessRetryDelay = TimeSpan.FromMilliseconds(50);
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
            await _FileAccessLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return await LoadCoreAsync(token).ConfigureAwait(false);
            }
            finally
            {
                _FileAccessLock.Release();
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

            await _FileAccessLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await SaveCoreAsync(profiles, token).ConfigureAwait(false);
            }
            finally
            {
                _FileAccessLock.Release();
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

            await _FileAccessLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                List<TerminalProfile> profiles = await LoadCoreAsync(token).ConfigureAwait(false);
                TerminalProfile? existing = profiles.FirstOrDefault(item => item.Id == profile.Id);
                if (existing != null)
                {
                    if (String.IsNullOrWhiteSpace(profile.FolderId) && !String.IsNullOrWhiteSpace(existing.FolderId))
                    {
                        profile.FolderId = existing.FolderId;
                    }

                    Int32 index = profiles.IndexOf(existing);
                    profiles[index] = profile;
                }
                else
                {
                    profiles.Add(profile);
                }

                await SaveCoreAsync(profiles, token).ConfigureAwait(false);
            }
            finally
            {
                _FileAccessLock.Release();
            }
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

            await _FileAccessLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                List<TerminalProfile> profiles = await LoadCoreAsync(token).ConfigureAwait(false);
                Int32 removed = profiles.RemoveAll(item => item.Id == profileId);
                await SaveCoreAsync(profiles, token).ConfigureAwait(false);
                return removed > 0;
            }
            finally
            {
                _FileAccessLock.Release();
            }
        }

        #endregion

        #region Private-Methods

        private async Task<List<TerminalProfile>> LoadCoreAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(FilePath)) return new List<TerminalProfile>();

            return await ExecuteWithFileAccessRetryAsync(async delegate
            {
                using FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                List<TerminalProfile>? profiles = await JsonSerializer.DeserializeAsync<List<TerminalProfile>>(
                    stream,
                    _JsonOptions,
                    token).ConfigureAwait(false);
                return NormalizeProfiles(profiles);
            }, token).ConfigureAwait(false);
        }

        private async Task SaveCoreAsync(List<TerminalProfile> profiles, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            Directory.CreateDirectory(_DirectoryPath);
            await ExecuteWithFileAccessRetryAsync(async delegate
            {
                using FileStream stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(stream, profiles, _JsonOptions, token).ConfigureAwait(false);
                return true;
            }, token).ConfigureAwait(false);
        }

        private static async Task<T> ExecuteWithFileAccessRetryAsync<T>(Func<Task<T>> action, CancellationToken token)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await action().ConfigureAwait(false);
                }
                catch (IOException) when (attempt < FileAccessRetryCount)
                {
                    await Task.Delay(FileAccessRetryDelay, token).ConfigureAwait(false);
                }
            }
        }

        private static string ResolveDirectoryPath(string? directoryPath)
        {
            if (!String.IsNullOrWhiteSpace(directoryPath)) return directoryPath;

            string? home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (String.IsNullOrWhiteSpace(home)) throw new InvalidOperationException("Unable to resolve the current user's home directory.");
            return Path.Combine(home, Constants.ApplicationDirectoryName);
        }

        private static List<TerminalProfile> NormalizeProfiles(List<TerminalProfile>? profiles)
        {
            if (profiles == null) return new List<TerminalProfile>();

            List<TerminalProfile> normalized = new List<TerminalProfile>();
            foreach (TerminalProfile? profile in profiles)
            {
                if (profile == null) continue;
                if (profile.GlobalColorScheme == null) profile.GlobalColorScheme = ColorSchemeCatalog.GetSchemes()[0];
                profile.FolderId ??= String.Empty;
                profile.Tabs ??= new List<TerminalTabProfile>();

                List<TerminalTabProfile> tabs = new List<TerminalTabProfile>();
                HashSet<string> tabIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (TerminalTabProfile? tab in profile.Tabs)
                {
                    if (tab == null) continue;
                    tab.EnsureId();
                    if (!tabIds.Add(tab.Id))
                    {
                        do
                        {
                            tab.RegenerateId();
                        }
                        while (!tabIds.Add(tab.Id));
                    }

                    tab.StartingDirectory ??= String.Empty;
                    tab.StartupScript ??= String.Empty;
                    tab.PtyRecordingDirectory ??= String.Empty;
                    tabs.Add(tab);
                }

                profile.Tabs = tabs;
                normalized.Add(profile);
            }

            return normalized;
        }

        #endregion
    }
}
