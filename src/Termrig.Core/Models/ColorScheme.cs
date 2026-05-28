namespace Termrig.Core.Models
{
    using System;

    /// <summary>
    /// Terminal color scheme.
    /// </summary>
    public class ColorScheme
    {
        #region Public-Members

        /// <summary>
        /// Scheme name.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Terminal background color as a hex value.
        /// </summary>
        public string Background
        {
            get
            {
                return _Background;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Background));
                _Background = value;
            }
        }

        /// <summary>
        /// Terminal foreground color as a hex value.
        /// </summary>
        public string Foreground
        {
            get
            {
                return _Foreground;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Foreground));
                _Foreground = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Name = "Termrig Dark";
        private string _Background = "#101419";
        private string _Foreground = "#E6EDF3";

        #endregion
    }
}
