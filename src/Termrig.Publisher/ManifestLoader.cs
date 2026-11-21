namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    /// <summary>Loads and validates publisher.json.</summary>
    public static class ManifestLoader
    {
        /// <summary>Reads a manifest file.</summary>
        /// <param name="path">Absolute path of publisher.json.</param>
        /// <returns>The validated manifest.</returns>
        /// <exception cref="PublisherException">The file is missing, malformed, or fails validation.</exception>
        public static PublisherManifest Load(string path)
        {
            if (!File.Exists(path)) throw new PublisherException("Manifest not found: " + path + ".");
            return Parse(File.ReadAllText(path));
        }

        /// <summary>Parses and validates manifest JSON.</summary>
        /// <param name="json">The manifest text.</param>
        /// <returns>The validated manifest.</returns>
        /// <exception cref="PublisherException">The JSON is malformed or fails validation.</exception>
        public static PublisherManifest Parse(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            PublisherManifest? manifest;
            try { manifest = JsonSerializer.Deserialize(json, PublisherManifestContext.Default.PublisherManifest); }
            catch (JsonException error) { throw new PublisherException("publisher.json is not valid: " + error.Message, error); }
            if (manifest == null) throw new PublisherException("publisher.json is empty.");
            Validate(manifest);
            return manifest;
        }

        /// <summary>Checks every field the channels depend on.</summary>
        /// <param name="manifest">The manifest to check.</param>
        /// <exception cref="PublisherException">A required value is missing or invalid; the message lists every problem.</exception>
        public static void Validate(PublisherManifest manifest)
        {
            ArgumentNullException.ThrowIfNull(manifest);
            List<string> problems = new List<string>();
            ProjectInfo project = manifest.Project;
            Require(problems, project.Name, "project.name");
            Require(problems, project.Command, "project.command");
            Require(problems, project.Identifier, "project.identifier");
            Require(problems, project.Tagline, "project.tagline");
            Require(problems, project.Description, "project.description");
            Require(problems, project.License, "project.license");
            Require(problems, project.Logo, "project.logo");
            Require(problems, project.Publisher, "project.publisher");
            Require(problems, project.PublisherUrl, "project.publisherUrl");
            Require(problems, project.RepositoryUrl, "project.repositoryUrl");
            Require(problems, manifest.Framework, "framework");
            if (project.Command.Length > 0 && (project.Command != project.Command.ToLowerInvariant() || project.Command.Any(character => !char.IsLetterOrDigit(character) && character != '-')))
                problems.Add("project.command must be lowercase letters, digits, and hyphens.");
            if (project.Identifier.Length > 0 && project.Identifier.Count(character => character == '.') < 2)
                problems.Add("project.identifier must be reverse-DNS, for example ai.latticetech." + project.Command + ".");
            if (manifest.Runtimes.Count == 0) problems.Add("runtimes must list at least one runtime identifier.");
            foreach (string rid in manifest.Runtimes)
            {
                try { RuntimeTarget.Parse(rid); }
                catch (PublisherException error) { problems.Add(error.Message); }
            }
            if (manifest.Runtimes.Distinct(StringComparer.Ordinal).Count() != manifest.Runtimes.Count) problems.Add("runtimes contains duplicates.");
            if (manifest.Artifacts.Count == 0) problems.Add("artifacts must list at least one executable.");
            foreach (ArtifactInfo artifact in manifest.Artifacts)
            {
                Require(problems, artifact.Name, "artifacts[].name");
                Require(problems, artifact.Project, "artifacts[].project");
                Require(problems, artifact.Executable, "artifacts[].executable");
                if (artifact.Kind != "cli" && artifact.Kind != "tray" && artifact.Kind != "desktop") problems.Add("artifact '" + artifact.Name + "' kind must be cli, tray, or desktop.");
            }
            int clis = manifest.Artifacts.Count(artifact => artifact.Kind == "cli");
            int desktops = manifest.Artifacts.Count(artifact => artifact.Kind == "desktop");
            if (clis > 1) problems.Add("At most one artifact may have kind cli; it is the command placed on PATH.");
            if (desktops > 1) problems.Add("At most one artifact may have kind desktop.");
            if (clis == 0 && desktops == 0) problems.Add("One artifact must have kind cli or desktop; it is the command placed on PATH.");
            if (manifest.Artifacts.Count(artifact => artifact.Kind == "tray") > 1) problems.Add("At most one artifact may have kind tray.");
            if (project.PathCommand.Length > 0 && (project.PathCommand != project.PathCommand.ToLowerInvariant() || project.PathCommand.Any(character => !char.IsLetterOrDigit(character) && character != '-')))
                problems.Add("project.pathCommand must be lowercase letters, digits, and hyphens.");
            foreach (string name in manifest.LaunchEnvironment.Keys)
                if (name.Length == 0 || name.Any(character => !char.IsLetterOrDigit(character) && character != '_')) problems.Add("launchEnvironment name '" + name + "' must be letters, digits, and underscores.");
            foreach (string value in manifest.LaunchEnvironment.Values)
                if (value.Any(character => char.IsWhiteSpace(character) || "\"'`$\\".Contains(character, StringComparison.Ordinal))) problems.Add("launchEnvironment value '" + value + "' cannot contain spaces, quotes, or shell characters.");
            if (manifest.Artifacts.Select(artifact => artifact.Executable.ToLowerInvariant()).Distinct(StringComparer.Ordinal).Count() != manifest.Artifacts.Count)
                problems.Add("Artifact executables must differ ignoring case, because macOS and Windows file systems are case-insensitive.");
            foreach (string channel in manifest.Channels)
            {
                if (channel == "publish" || !ChannelCatalog.All.Contains(channel, StringComparer.Ordinal)) problems.Add("Unknown channel '" + channel + "'.");
            }
            if (manifest.Artifacts.Any(artifact => artifact.Kind == "tray"))
            {
                Require(problems, manifest.Agent.Label, "agent.label");
                Require(problems, manifest.Agent.WindowsName, "agent.windowsName");
            }
            Require(problems, manifest.Linux.InstallDirectory, "linux.installDirectory");
            if (manifest.Signing.Macos.IsConfigured) Require(problems, manifest.Signing.Macos.Identity, "signing.macos.identity");
            if (manifest.Signing.Windows.IsConfigured)
            {
                Require(problems, manifest.Signing.Windows.CertificateEnvironment, "signing.windows.certificateEnvironment");
                Require(problems, manifest.Signing.Windows.PasswordEnvironment, "signing.windows.passwordEnvironment");
            }
            if (problems.Count > 0) throw new PublisherException("publisher.json has problems:" + Environment.NewLine + "  - " + string.Join(Environment.NewLine + "  - ", problems));
        }

        /// <summary>Finds publisher.json by walking up from a directory.</summary>
        /// <param name="start">Directory to start from.</param>
        /// <returns>The directory that holds publisher.json.</returns>
        /// <exception cref="PublisherException">No ancestor holds publisher.json.</exception>
        public static string FindRoot(string start)
        {
            DirectoryInfo? directory = new DirectoryInfo(start);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "publisher.json"))) return directory.FullName;
                directory = directory.Parent;
            }
            throw new PublisherException("publisher.json not found in " + start + " or any parent directory. Pass --root.");
        }

        private static void Require(List<string> problems, string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) problems.Add(name + " is required.");
        }
    }
}
