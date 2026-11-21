namespace Termrig.Publisher
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Xml;
    using System.Xml.Linq;

    /// <summary>Reads the project version from its single source file.</summary>
    public static class VersionReader
    {
        private static readonly Regex _SemanticVersion = new Regex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", RegexOptions.CultureInvariant);

        /// <summary>Reads the first <c>Version</c> element from an MSBuild file.</summary>
        /// <param name="path">Absolute path of the MSBuild file.</param>
        /// <returns>The validated semantic version.</returns>
        /// <exception cref="PublisherException">The file is missing, malformed, or holds no valid version.</exception>
        public static string Read(string path)
        {
            if (!File.Exists(path)) throw new PublisherException("Version file not found: " + path + ".");
            XDocument document;
            try { document = XDocument.Load(path); }
            catch (XmlException error) { throw new PublisherException("Version file " + path + " is not valid XML.", error); }
            XElement? element = document.Descendants().FirstOrDefault(node => node.Name.LocalName == "Version");
            if (element == null) throw new PublisherException("No <Version> element in " + path + ".");
            return Validate(element.Value.Trim());
        }

        /// <summary>Validates a semantic version string such as <c>1.4.0</c> or <c>0.6.0-alpha</c>.</summary>
        /// <param name="version">The version to check.</param>
        /// <returns>The same version.</returns>
        /// <exception cref="PublisherException">The value is not MAJOR.MINOR.PATCH with an optional pre-release suffix.</exception>
        public static string Validate(string version)
        {
            ArgumentNullException.ThrowIfNull(version);
            if (!_SemanticVersion.IsMatch(version)) throw new PublisherException("'" + version + "' is not a semantic version (MAJOR.MINOR.PATCH, optionally -suffix).");
            return version;
        }

        /// <summary>Returns true when a version should be published as a GitHub pre-release.</summary>
        /// <param name="version">A validated semantic version.</param>
        /// <param name="prereleaseBeforeOne">True to treat every 0.x.y version as alpha.</param>
        /// <returns>True for a suffixed version, or a 0.x.y version when <paramref name="prereleaseBeforeOne"/> is set.</returns>
        public static bool IsPrerelease(string version, bool prereleaseBeforeOne)
        {
            ArgumentNullException.ThrowIfNull(version);
            return version.Contains('-', StringComparison.Ordinal) || (prereleaseBeforeOne && version.StartsWith("0.", StringComparison.Ordinal));
        }
    }
}
