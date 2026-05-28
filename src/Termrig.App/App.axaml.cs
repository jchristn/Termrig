namespace Termrig.App
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Markup.Xaml;
    using Avalonia.Threading;
    using System;
    using System.Threading.Tasks;
    using Termrig.App.Services;
    using Termrig.Core.Services;
    using Termrig.App.Views;

    /// <summary>
    /// Termrig application bootstrapper.
    /// </summary>
    public partial class App : Application
    {
        #region Private-Members

        private readonly CrashLogStore _CrashLogStore = new CrashLogStore();
        private TermrigCommandServer? _CommandServer = null;
        private MainWindow? _MainWindow = null;

        #endregion

        /// <summary>
        /// Initialize application XAML.
        /// </summary>
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            RegisterCrashHandlers();
        }

        /// <summary>
        /// Complete framework initialization.
        /// </summary>
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                CommandLineCommand.TryParse(desktop.Args ?? Array.Empty<string>(), out CommandLineCommand? startupCommand);
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                SplashWindow? splash = null;
                splash = new SplashWindow(delegate
                {
                    MainWindow main = new MainWindow();
                    _MainWindow = main;
                    desktop.MainWindow = main;
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    main.Show();
                    splash?.Close();
                    StartCommandServer();
                    if (startupCommand != null && String.Equals(startupCommand.Verb, "open", StringComparison.Ordinal))
                    {
                        _ = main.OpenProfileByNameAsync(startupCommand.ProfileName);
                    }
                });
                desktop.MainWindow = splash;
                desktop.Exit += delegate
                {
                    _CommandServer?.Dispose();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void RegisterCrashHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void StartCommandServer()
        {
            if (_CommandServer != null) return;
            _CommandServer = new TermrigCommandServer(HandleCommandAsync);
            _CommandServer.Start();
        }

        private Task<bool> HandleCommandAsync(CommandLineCommand command)
        {
            return Dispatcher.UIThread.InvokeAsync(async delegate
            {
                if (_MainWindow == null) return false;
                if (String.Equals(command.Verb, "open", StringComparison.Ordinal))
                {
                    return await _MainWindow.OpenProfileByNameAsync(command.ProfileName).ConfigureAwait(true);
                }

                if (String.Equals(command.Verb, "close", StringComparison.Ordinal))
                {
                    return _MainWindow.CloseProfileWorkspaces(command.ProfileName);
                }

                return false;
            });
        }

        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception? exception = e.ExceptionObject as Exception;
            string details = exception == null ? e.ExceptionObject?.ToString() ?? "Unknown application crash." : exception.ToString();
            _CrashLogStore.Write("Termrig", "application", "Unhandled application exception.", details);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _CrashLogStore.Write("Termrig", "application", "Unobserved task exception.", e.Exception.ToString());
            e.SetObserved();
        }
    }
}
