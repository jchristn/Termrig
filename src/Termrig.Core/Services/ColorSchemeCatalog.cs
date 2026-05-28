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
                new ColorScheme { Name = "High Contrast", Background = "#000000", Foreground = "#FFFFFF" }
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
            return match ?? schemes[0];
        }
    }
}
