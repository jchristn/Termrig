namespace Termrig.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Imports the user's login-shell environment into the Termrig process on macOS.
    /// Apps launched from Finder or the Dock inherit a minimal launchd PATH (/usr/bin:/bin:/usr/sbin:/sbin),
    /// so tools installed via ~/.zprofile, ~/.zshrc, Homebrew, etc. would otherwise be missing from terminal tabs.
    /// </summary>
    public static class LoginShellEnvironment
    {
        #region Private-Members

        private const string Marker = "__TERMRIG_PATH__";
        private const int TimeoutMilliseconds = 5000;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Apply the login-shell PATH and macOS shell defaults to the current process environment.
        /// Child terminal processes inherit the result. No-op on platforms other than macOS.
        /// </summary>
        public static void Apply()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return;

            // Suppress Apple's "default interactive shell is now zsh" banner in bash tabs.
            Environment.SetEnvironmentVariable("BASH_SILENCE_DEPRECATION_WARNING", "1");

            try
            {
                string? loginPath = ReadLoginShellPath();
                if (String.IsNullOrWhiteSpace(loginPath)) return;

                string? currentPath = Environment.GetEnvironmentVariable("PATH");
                Environment.SetEnvironmentVariable("PATH", MergePaths(loginPath, currentPath));
            }
            catch
            {
                // Best effort: fall back to the inherited environment.
            }
        }

        /// <summary>
        /// Merge two PATH values, preserving the order of the first and appending unique entries from the second.
        /// </summary>
        /// <param name="primary">Preferred PATH value.</param>
        /// <param name="secondary">Additional PATH value.</param>
        /// <returns>Merged PATH value.</returns>
        public static string MergePaths(string? primary, string? secondary)
        {
            List<string> entries = new List<string>();
            foreach (string value in new string?[] { primary, secondary }.Where(item => !String.IsNullOrWhiteSpace(item))!)
            {
                foreach (string entry in value.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!entries.Contains(entry, StringComparer.Ordinal)) entries.Add(entry);
                }
            }

            return String.Join(Path.PathSeparator, entries);
        }

        #endregion

        #region Private-Methods

        private static string? ReadLoginShellPath()
        {
            string shell = Environment.GetEnvironmentVariable("SHELL") ?? String.Empty;
            if (String.IsNullOrWhiteSpace(shell) || !File.Exists(shell)) shell = "/bin/zsh";
            if (!File.Exists(shell)) return null;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = shell,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };
            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("printf '" + Marker + "%s" + Marker + "' \"$PATH\"");

            using Process? process = Process.Start(startInfo);
            if (process == null) return null;

            process.StandardInput.Close();
            process.ErrorDataReceived += (sender, e) => { };
            process.BeginErrorReadLine();
            System.Threading.Tasks.Task<string> output = process.StandardOutput.ReadToEndAsync();

            if (!process.WaitForExit(TimeoutMilliseconds))
            {
                try
                {
                    process.Kill(true);
                }
                catch
                {
                }

                return null;
            }

            string text = output.GetAwaiter().GetResult();
            int start = text.IndexOf(Marker, StringComparison.Ordinal);
            if (start < 0) return null;
            start += Marker.Length;
            int end = text.IndexOf(Marker, start, StringComparison.Ordinal);
            if (end < 0) return null;
            return text.Substring(start, end - start);
        }

        #endregion
    }
}
