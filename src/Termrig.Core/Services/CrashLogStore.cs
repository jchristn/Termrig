namespace Termrig.Core.Services
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Stores application and terminal crash logs under ~/.termlog/crashes.
    /// </summary>
    public class CrashLogStore
    {
        #region Public-Members

        /// <summary>
        /// Directory containing crash log files.
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

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the crash log store.
        /// </summary>
        /// <param name="directoryPath">Optional crash log directory. Defaults to ~/.termlog/crashes.</param>
        public CrashLogStore(string? directoryPath = null)
        {
            _DirectoryPath = ResolveDirectoryPath(directoryPath);
            Directory.CreateDirectory(_DirectoryPath);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Write a crash log.
        /// </summary>
        /// <param name="profileName">Profile name.</param>
        /// <param name="tabName">Tab name.</param>
        /// <param name="summary">Crash summary.</param>
        /// <param name="details">Crash details.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created log file path.</returns>
        public async Task<string> WriteAsync(string? profileName, string? tabName, string summary, string details, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(summary)) throw new ArgumentNullException(nameof(summary));
            if (String.IsNullOrWhiteSpace(details)) throw new ArgumentNullException(nameof(details));
            token.ThrowIfCancellationRequested();

            Directory.CreateDirectory(_DirectoryPath);
            string filePath = BuildFilePath(profileName, tabName);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Timestamp: " + DateTimeOffset.Now.ToString("O"));
            builder.AppendLine("Profile: " + NormalizeLogValue(profileName, "application"));
            builder.AppendLine("Tab: " + NormalizeLogValue(tabName, "application"));
            builder.AppendLine("Summary: " + summary);
            builder.AppendLine();
            builder.AppendLine(details);

            await File.WriteAllTextAsync(filePath, builder.ToString(), token).ConfigureAwait(false);
            return filePath;
        }

        /// <summary>
        /// Write a crash log synchronously.
        /// </summary>
        /// <param name="profileName">Profile name.</param>
        /// <param name="tabName">Tab name.</param>
        /// <param name="summary">Crash summary.</param>
        /// <param name="details">Crash details.</param>
        /// <returns>Created log file path.</returns>
        public string Write(string? profileName, string? tabName, string summary, string details)
        {
            if (String.IsNullOrWhiteSpace(summary)) throw new ArgumentNullException(nameof(summary));
            if (String.IsNullOrWhiteSpace(details)) throw new ArgumentNullException(nameof(details));

            Directory.CreateDirectory(_DirectoryPath);
            string filePath = BuildFilePath(profileName, tabName);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Timestamp: " + DateTimeOffset.Now.ToString("O"));
            builder.AppendLine("Profile: " + NormalizeLogValue(profileName, "application"));
            builder.AppendLine("Tab: " + NormalizeLogValue(tabName, "application"));
            builder.AppendLine("Summary: " + summary);
            builder.AppendLine();
            builder.AppendLine(details);

            File.WriteAllText(filePath, builder.ToString());
            return filePath;
        }

        #endregion

        #region Private-Methods

        private string BuildFilePath(string? profileName, string? tabName)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string filename = timestamp + "-" + SanitizeName(profileName, "application") + "-" + SanitizeName(tabName, "application") + ".log";
            return Path.Combine(_DirectoryPath, filename);
        }

        private static string NormalizeLogValue(string? value, string fallback)
        {
            return String.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string SanitizeName(string? value, string fallback)
        {
            string normalized = NormalizeLogValue(value, fallback).Trim();
            StringBuilder builder = new StringBuilder();
            foreach (char character in normalized)
            {
                if (Char.IsLetterOrDigit(character) || character == '-' || character == '_')
                {
                    builder.Append(character);
                }
                else if (Char.IsWhiteSpace(character))
                {
                    builder.Append('_');
                }
            }

            if (builder.Length < 1) return fallback;
            return builder.ToString();
        }

        private static string ResolveDirectoryPath(string? directoryPath)
        {
            if (!String.IsNullOrWhiteSpace(directoryPath)) return directoryPath;

            string? home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (String.IsNullOrWhiteSpace(home)) throw new InvalidOperationException("Unable to resolve the current user's home directory.");
            return Path.Combine(home, Constants.CrashLogDirectoryName, Constants.CrashLogSubdirectoryName);
        }

        #endregion
    }
}
