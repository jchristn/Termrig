namespace Termrig.App.Views
{
    using Avalonia.Controls;
    using Avalonia.Threading;
    using System;

    /// <summary>
    /// Startup splash screen.
    /// </summary>
    public partial class SplashWindow : Window
    {
        #region Private-Members

        private readonly Action _Completed;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the splash window for the XAML loader.
        /// </summary>
        public SplashWindow()
            : this(delegate { })
        {
        }

        /// <summary>
        /// Instantiate the splash window.
        /// </summary>
        /// <param name="completed">Action invoked when the splash has finished.</param>
        /// <exception cref="ArgumentNullException">Thrown when completed is null.</exception>
        public SplashWindow(Action completed)
        {
            ArgumentNullException.ThrowIfNull(completed);

            _Completed = completed;
            InitializeComponent();
            Opened += OnOpened;
        }

        #endregion

        #region Private-Methods

        private void OnOpened(object? sender, EventArgs e)
        {
            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1400)
            };
            timer.Tick += delegate
            {
                timer.Stop();
                _Completed.Invoke();
            };
            timer.Start();
        }

        #endregion
    }
}
