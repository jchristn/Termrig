namespace Termrig.App.Services
{
    using Avalonia.Media;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Resolves terminal font families, preferring the fonts bundled with Termrig so terminals render
    /// the same on Windows and macOS regardless of which fonts are installed.
    /// </summary>
    public static class TerminalFonts
    {
        #region Public-Members

        /// <summary>
        /// Default terminal font family, bundled with Termrig.
        /// </summary>
        public const string DefaultFamilyName = "Cascadia Mono";

        #endregion

        #region Private-Members

        private const string BundledFontsUri = "avares://Termrig/Assets/Fonts";

        private static readonly string[] _BundledFamilies = new string[]
        {
            "Cascadia Code",
            "Cascadia Mono"
        };

        // Bundled symbol fonts that cover glyphs common in TUIs (e.g. Claude Code's ⏺ ⎿ ⏵) which
        // monospace fonts usually lack.
        private static readonly string[] _BundledSymbolFamilies = new string[]
        {
            "Noto Sans Symbols 2",
            "Noto Sans Symbols"
        };

        private static HashSet<string>? _InstalledFamilies = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the font family used to render a terminal.
        /// The requested family is used when it is bundled or installed; otherwise the bundled default is substituted.
        /// Bundled symbol fonts and platform symbol fonts are appended as fallbacks.
        /// </summary>
        /// <param name="requested">Requested family name, optionally a comma-separated list where the first entry is used.</param>
        /// <returns>Font family with fallbacks.</returns>
        public static FontFamily Resolve(string? requested)
        {
            return FontFamily.Parse(BuildFamilyList(requested));
        }

        /// <summary>
        /// Build the comma-separated font family list for a requested family.
        /// </summary>
        /// <param name="requested">Requested family name.</param>
        /// <returns>Font family list.</returns>
        public static string BuildFamilyList(string? requested)
        {
            string name = (requested ?? String.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? String.Empty;

            List<string> families = new List<string>();
            if (IsBundled(name))
            {
                families.Add(Bundled(name));
            }
            else if (!String.IsNullOrWhiteSpace(name) && IsInstalled(name))
            {
                families.Add(name);
            }
            else
            {
                families.Add(Bundled(DefaultFamilyName));
            }

            families.AddRange(_BundledSymbolFamilies.Select(Bundled));
            families.AddRange(GetPlatformSymbolFamilies());
            return String.Join(", ", families.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determine whether a family is bundled with Termrig.
        /// </summary>
        /// <param name="name">Family name.</param>
        /// <returns>True if bundled.</returns>
        public static bool IsBundled(string? name)
        {
            return _BundledFamilies.Contains(name ?? String.Empty, StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Private-Methods

        private static string Bundled(string name)
        {
            string canonical = _BundledFamilies.Concat(_BundledSymbolFamilies)
                .First(item => String.Equals(item, name, StringComparison.OrdinalIgnoreCase));
            return BundledFontsUri + "#" + canonical;
        }

        private static bool IsInstalled(string name)
        {
            if (_InstalledFamilies == null)
            {
                HashSet<string> installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (FontFamily family in FontManager.Current.SystemFonts)
                    {
                        installed.Add(family.Name);
                    }
                }
                catch
                {
                }

                _InstalledFamilies = installed;
            }

            return _InstalledFamilies.Contains(name);
        }

        private static IEnumerable<string> GetPlatformSymbolFamilies()
        {
            if (OperatingSystem.IsMacOS()) return new string[] { "Menlo", "STIX Two Math", "Apple Symbols" };
            if (OperatingSystem.IsWindows()) return new string[] { "Segoe UI Symbol", "Consolas" };
            return new string[] { "DejaVu Sans Mono" };
        }

        #endregion
    }
}
