namespace Termrig.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Process launch details for a terminal tab.
    /// </summary>
    public class ShellLaunchPlan
    {
        #region Public-Members

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

        /// <summary>
        /// Command-line arguments.
        /// </summary>
        public List<string> Arguments { get; set; } = new List<string>();

        /// <summary>
        /// Working directory.
        /// </summary>
        public string? StartingDirectory { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Executable = String.Empty;

        #endregion
    }
}
