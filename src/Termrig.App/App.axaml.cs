namespace Termrig.App
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Markup.Xaml;
    using System;
    using System.Threading.Tasks;
    using Termrig.Core.Services;
    using Termrig.App.Views;

    /// <summary>
    /// Termrig application bootstrapper.
    /// </summary>
    public partial class App : Application
    {
        #region Private-Members

        private readonly CrashLogStore _CrashLogStore = new CrashLogStore();

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
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                SplashWindow? splash = null;
                splash = new SplashWindow(delegate
                {
                    MainWindow main = new MainWindow();
                    desktop.MainWindow = main;
                    desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    main.Show();
                    splash?.Close();
                });
                desktop.MainWindow = splash;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void RegisterCrashHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
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
