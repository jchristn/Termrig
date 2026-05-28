namespace Termrig.App
{
    using Avalonia;
    using System;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>
    /// Application entry point.
    /// </summary>
    public sealed class Program
    {
        #region Private-Members

        private const string DetachedChildArgument = "--termrig-detached-child";
        private const string ForegroundEnvironmentVariable = "TERMRIG_FOREGROUND";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        [STAThread]
        public static void Main(string[] args)
        {
            if (ShouldDetach(args))
            {
                StartDetachedChild(args);
                return;
            }

            string[] avaloniaArgs = args.Where(argument => argument != DetachedChildArgument).ToArray();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(avaloniaArgs);
        }

        /// <summary>
        /// Build the Avalonia application.
        /// </summary>
        /// <returns>App builder.</returns>
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
        }

        #endregion

        #region Private-Methods

        private static bool ShouldDetach(string[] args)
        {
            string? foreground = Environment.GetEnvironmentVariable(ForegroundEnvironmentVariable);
            return !args.Contains(DetachedChildArgument) &&
                !String.Equals(foreground, "1", StringComparison.Ordinal);
        }

        private static void StartDetachedChild(string[] args)
        {
            string? executable = Environment.ProcessPath;
            if (String.IsNullOrWhiteSpace(executable))
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                return;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory
            };

            startInfo.ArgumentList.Add(DetachedChildArgument);
            foreach (string argument in args)
            {
                startInfo.ArgumentList.Add(argument);
            }
 
            Process? process = Process.Start(startInfo);
            process?.Dispose();
        }

        #endregion
    }
}
