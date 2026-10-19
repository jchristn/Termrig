namespace Termrig.Core.Models
{
    /// <summary>Preferences shared by all terminal workspaces.</summary>
    public sealed class ApplicationSettings
    {
        public bool UpdateUnfocusedWindows { get; set; } = true;
    }
}
