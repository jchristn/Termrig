namespace Termrig.App.Services
{
    using System;
    using System.Linq;

    /// <summary>
    /// Parsed command submitted through the tr command.
    /// </summary>
    public sealed class CommandLineCommand
    {
        #region Public-Members

        /// <summary>
        /// Command verb.
        /// </summary>
        public string Verb { get; }

        /// <summary>
        /// Profile name associated with the command.
        /// </summary>
        public string ProfileName { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the command.
        /// </summary>
        /// <param name="verb">Command verb.</param>
        /// <param name="profileName">Profile name.</param>
        public CommandLineCommand(string verb, string profileName)
        {
            if (String.IsNullOrWhiteSpace(verb)) throw new ArgumentNullException(nameof(verb));
            if (String.IsNullOrWhiteSpace(profileName)) throw new ArgumentNullException(nameof(profileName));

            Verb = verb.Trim().ToLowerInvariant();
            ProfileName = profileName.Trim();
        }

        /// <summary>
        /// Try to parse command-line arguments.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <param name="command">Parsed command.</param>
        /// <returns>True if a supported command was parsed.</returns>
        public static bool TryParse(string[] args, out CommandLineCommand? command)
        {
            command = null;
            string[] filtered = args
                .Where(item => !String.Equals(item, Program.DetachedChildArgument, StringComparison.Ordinal))
                .ToArray();

            if (filtered.Length < 2) return false;
            string verb = filtered[0].Trim();
            if (!String.Equals(verb, "open", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(verb, "close", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string profileName = String.Join(" ", filtered.Skip(1)).Trim();
            if (String.IsNullOrWhiteSpace(profileName)) return false;

            command = new CommandLineCommand(verb, profileName);
            return true;
        }

        /// <summary>
        /// Serialize the command for local IPC.
        /// </summary>
        /// <returns>Serialized command.</returns>
        public string Serialize()
        {
            return Verb + "\t" + ProfileName;
        }

        /// <summary>
        /// Try to deserialize a command from local IPC.
        /// </summary>
        /// <param name="value">Serialized command.</param>
        /// <param name="command">Parsed command.</param>
        /// <returns>True if parsed.</returns>
        public static bool TryDeserialize(string? value, out CommandLineCommand? command)
        {
            command = null;
            if (String.IsNullOrWhiteSpace(value)) return false;

            string[] parts = value.Split('\t', 2, StringSplitOptions.None);
            if (parts.Length != 2) return false;
            if (String.IsNullOrWhiteSpace(parts[0]) || String.IsNullOrWhiteSpace(parts[1])) return false;

            command = new CommandLineCommand(parts[0], parts[1]);
            return true;
        }

        #endregion
    }
}
