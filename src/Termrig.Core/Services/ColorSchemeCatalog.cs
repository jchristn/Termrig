namespace Termrig.Core.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using Termrig.Core.Models;

    /// <summary>
    /// Provides built-in terminal color schemes.
    /// </summary>
    public static class ColorSchemeCatalog
    {
        /// <summary>
        /// Get built-in color schemes.
        /// </summary>
        /// <returns>Color schemes.</returns>
        public static List<ColorScheme> GetSchemes()
        {
            return new List<ColorScheme>
            {
                new ColorScheme { Name = "Termrig Dark", Background = "#101419", Foreground = "#E6EDF3" },
                new ColorScheme { Name = "Ink", Background = "#0B0F14", Foreground = "#F4F7FA" },
                new ColorScheme { Name = "Slate", Background = "#17202A", Foreground = "#DDE7F0" },
                new ColorScheme { Name = "Light", Background = "#FBFBF8", Foreground = "#222222" },
                new ColorScheme { Name = "High Contrast", Background = "#000000", Foreground = "#FFFFFF" },
                new ColorScheme { Name = "VS Code Dark+", Background = "#1E1E1E", Foreground = "#D4D4D4" },
                new ColorScheme { Name = "VS Code Light+", Background = "#FFFFFF", Foreground = "#333333" },
                new ColorScheme { Name = "Visual Studio Dark", Background = "#1E1E1E", Foreground = "#DCDCDC" },
                new ColorScheme { Name = "Visual Studio Blue", Background = "#293955", Foreground = "#F1F5FB" },
                new ColorScheme { Name = "Ubuntu Terminal", Background = "#300A24", Foreground = "#EEEEEC" },
                new ColorScheme { Name = "Tango Dark", Background = "#000000", Foreground = "#D3D7CF" },
                new ColorScheme { Name = "Solarized Dark", Background = "#002B36", Foreground = "#839496" },
                new ColorScheme { Name = "Solarized Light", Background = "#FDF6E3", Foreground = "#657B83" },
                new ColorScheme { Name = "One Dark", Background = "#282C34", Foreground = "#ABB2BF" },
                new ColorScheme { Name = "Monokai", Background = "#272822", Foreground = "#F8F8F2" }
            };
        }

        /// <summary>
        /// Find a scheme by name, or return the default scheme.
        /// </summary>
        /// <param name="name">Scheme name.</param>
        /// <returns>Matching scheme or default scheme.</returns>
        public static ColorScheme FindByName(string? name)
        {
            List<ColorScheme> schemes = GetSchemes();
            ColorScheme? match = schemes.FirstOrDefault(item => item.Name == name);
            return Clone(match ?? schemes[0]);
        }

        /// <summary>
        /// Create a detached copy of a color scheme.
        /// </summary>
        /// <param name="scheme">Scheme to clone.</param>
        /// <returns>Cloned scheme.</returns>
        public static ColorScheme Clone(ColorScheme scheme)
        {
            return new ColorScheme
            {
                Name = scheme.Name,
                Background = scheme.Background,
                Foreground = scheme.Foreground
            };
        }
    }
}
