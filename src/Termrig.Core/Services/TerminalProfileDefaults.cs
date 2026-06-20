namespace Termrig.Core.Services
{
    using System;
    using Termrig.Core.Enums;
    using Termrig.Core.Models;

    /// <summary>
    /// Applies terminal profile defaults that depend on selected shells.
    /// </summary>
    public static class TerminalProfileDefaults
    {
        /// <summary>
        /// Applies the default profile font for the selected shell.
        /// </summary>
        /// <param name="profile">Profile to update.</param>
        /// <param name="shell">Selected shell.</param>
        /// <returns>True if the profile was changed.</returns>
        public static bool ApplyShellFontDefaults(TerminalProfile profile, ShellType shell)
        {
            ArgumentNullException.ThrowIfNull(profile);

            if (shell == ShellType.Cmd && String.IsNullOrWhiteSpace(profile.FontFamily))
            {
                profile.FontFamily = Constants.CmdTerminalFontFamily;
                return true;
            }

            return false;
        }
    }
}
