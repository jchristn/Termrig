namespace Termrig.App
{
    using Avalonia;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Termrig.App.Services;
    using Termrig.Core.Services;

    /// <summary>
    /// Application entry point.
    /// </summary>
    public sealed class Program
    {
        #region Private-Members

        internal const string DetachedChildArgument = "--termrig-detached-child";
        private const string ForegroundEnvironmentVariable = "TERMRIG_FOREGROUND";

        // Keep in sync with GUI_LIBS in scripts/linux/*.sh, scripts/wsl*/_wsl-common.sh and
        // packaging/linux/build-app.sh, and with "Desktop libraries" in INSTALLING_ON_LINUX.md.
        private const string DebianDesktopPackages =
            "libx11-6 libice6 libsm6 libxext6 libxrandr2 libxi6 libxcursor1 libxfixes3 libfontconfig1";
        private const string FedoraDesktopPackages =
            "libX11 libICE libSM libXext libXrandr libXi libXcursor libXfixes fontconfig";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        [STAThread]
        public static void Main(string[] args)
        {
            // Ensure the thread pool starts with enough ready threads that a burst of terminal
            // writes / process launches never has to wait for the pool's slow (~1-2/sec) thread
            // injection. PTY *reads* run on dedicated threads (see TerminalView), so this only needs
            // to cover the short-lived write/continuation work.
            ThreadPool.GetMinThreads(out int minWorker, out int minIo);
            ThreadPool.SetMinThreads(Math.Max(minWorker, 32), Math.Max(minIo, 32));

            if (CommandLineCommand.TryParse(args, out CommandLineCommand? command) &&
                command != null &&
                !args.Contains(DetachedChildArgument))
            {
                bool sent = TermrigCommandClient.TrySendAsync(command).GetAwaiter().GetResult();
                if (sent || String.Equals(command.Verb, "close", StringComparison.Ordinal))
                {
                    return;
                }
            }

            if (ShouldDetach(args))
            {
                StartDetachedChild(args);
                return;
            }

            LoginShellEnvironment.Apply();

            string[] avaloniaArgs = args.Where(argument => argument != DetachedChildArgument).ToArray();
            RunAvaloniaApp(avaloniaArgs);
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
                RunAvaloniaApp(args);
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

        private static void RunAvaloniaApp(string[] args)
        {
            try
            {
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception exception)
            {
                ApplicationCrashLogWriter.TryWrite("Unhandled application exception.", exception.ToString());
                DllNotFoundException? missingLibrary = FindMissingLibrary(exception);
                if (OperatingSystem.IsLinux() && missingLibrary != null)
                {
                    // Avalonia's X11 backend and SkiaSharp load these at startup. WSL distros and
                    // minimal images often lack them; say how to install them instead of a stack trace.
                    Console.Error.WriteLine("Termrig could not open its window: " + missingLibrary.Message);
                    Console.Error.WriteLine("Termrig needs the X11 and fontconfig libraries. Install them with:");
                    Console.Error.WriteLine("  Debian/Ubuntu (and WSL): sudo apt install " + DebianDesktopPackages);
                    Console.Error.WriteLine("  Fedora:                  sudo dnf install " + FedoraDesktopPackages);
                    Console.Error.WriteLine("See \"Desktop libraries\" in INSTALLING_ON_LINUX.md.");
                    Environment.Exit(1);
                }

                throw;
            }
        }

        private static DllNotFoundException? FindMissingLibrary(Exception? exception)
        {
            while (exception != null)
            {
                if (exception is DllNotFoundException missing)
                {
                    return missing;
                }

                exception = exception.InnerException;
            }

            return null;
        }

        #endregion
    }
}
