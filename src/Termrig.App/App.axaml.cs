namespace Termrig.App
{
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.ApplicationLifetimes;
    using Avalonia.Markup.Xaml;
    using Termrig.App.Views;

    /// <summary>
    /// Termrig application bootstrapper.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initialize application XAML.
        /// </summary>
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
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
    }
}
