namespace Termrig.Core.Models
{
    using System;
    using Termrig.Core.Enums;

    /// <summary>
    /// Describes an available shell on the current host.
    /// </summary>
    public class ShellDescriptor
    {
        #region Public-Members

        /// <summary>
        /// Shell type.
        /// </summary>
        public ShellType Shell { get; set; }

        /// <summary>
        /// Friendly shell display name.
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
        /// Executable path or command.
        /// </summary>
        public string Executable
        {
            get
            {
                return _Executable;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Executable));
                _Executable = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Name = String.Empty;
        private string _Executable = String.Empty;

        #endregion
    }
}
