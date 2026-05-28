namespace Termrig.Core.Enums
{
    /// <summary>
    /// Supported terminal shell types.
    /// </summary>
    public enum ShellType
    {
        /// <summary>
        /// Windows Command Prompt.
        /// </summary>
        Cmd = 0,

        /// <summary>
        /// Windows PowerShell or PowerShell Core.
        /// </summary>
        PowerShell = 1,

        /// <summary>
        /// Bash shell on macOS or Linux.
        /// </summary>
        Bash = 2
    }
}
