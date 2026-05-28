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
    /// Stores editable global color schemes in JSON under the user's home directory.
    /// </summary>
    public class ColorSchemeStore
    {
        #region Public-Members

        /// <summary>
        /// Directory containing Termrig color scheme data.
        /// </summary>
        public string DirectoryPath
        {
            get
            {
                return _DirectoryPath;
            }
        }

        /// <summary>
        /// Full color scheme JSON file path.
        /// </summary>
        public string FilePath
        {
            get
            {
                return Path.Combine(_DirectoryPath, Constants.ColorSchemesFilename);
            }
        }

        #endregion

        #region Private-Members

        private readonly string _DirectoryPath;
        private readonly JsonSerializerOptions _JsonOptions;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the color scheme store.
        /// </summary>
        /// <param name="directoryPath">Optional storage directory. Defaults to ~/.termrig.</param>
        public ColorSchemeStore(string? directoryPath = null)
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
        /// Load all global color schemes, falling back to built-in defaults when no user store exists.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Available color schemes.</returns>
        public async Task<List<ColorScheme>> LoadAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(FilePath)) return ColorSchemeCatalog.GetSchemes();

            using (FileStream stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                List<ColorScheme>? schemes = await JsonSerializer.DeserializeAsync<List<ColorScheme>>(
                    stream,
                    _JsonOptions,
                    token).ConfigureAwait(false);

                if (schemes == null || schemes.Count < 1) return ColorSchemeCatalog.GetSchemes();
                List<ColorScheme> normalized = Normalize(schemes);
                if (normalized.Count < 1) return ColorSchemeCatalog.GetSchemes();
                return normalized;
            }
        }

        /// <summary>
        /// Save all global color schemes.
        /// </summary>
        /// <param name="schemes">Schemes to write.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when schemes is null.</exception>
        public async Task SaveAsync(List<ColorScheme> schemes, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(schemes);
            token.ThrowIfCancellationRequested();

            List<ColorScheme> normalized = Normalize(schemes);
            Directory.CreateDirectory(_DirectoryPath);
            using (FileStream stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, _JsonOptions, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Reset global color schemes back to built-in defaults.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Default color schemes.</returns>
        public async Task<List<ColorScheme>> ResetDefaultsAsync(CancellationToken token = default)
        {
            List<ColorScheme> defaults = ColorSchemeCatalog.GetSchemes();
            await SaveAsync(defaults, token).ConfigureAwait(false);
            return defaults;
        }

        #endregion

        #region Private-Methods

        private static List<ColorScheme> Normalize(List<ColorScheme> schemes)
        {
            return schemes
                .Where(item =>
                    item != null &&
                    !String.IsNullOrWhiteSpace(item.Name) &&
                    !String.IsNullOrWhiteSpace(item.Background) &&
                    !String.IsNullOrWhiteSpace(item.Foreground))
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => ColorSchemeCatalog.Clone(group.Last()))
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
