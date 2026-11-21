namespace Termrig.Publisher
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Parsed command-line options.</summary>
    public sealed class PublisherOptions
    {
        /// <summary>Channels to run, in order.</summary>
        public List<string> Channels { get; } = new List<string>();

        /// <summary>True to run every enabled channel the host supports, then checksums.</summary>
        public bool All { get; set; }

        /// <summary>Version override; null reads the version file.</summary>
        public string? Version { get; set; }

        /// <summary>Repository root override; null searches upward for publisher.json.</summary>
        public string? Root { get; set; }

        /// <summary>Output directory override; null uses <c>installers/&lt;version&gt;</c>.</summary>
        public string? Output { get; set; }

        /// <summary>Runtime identifiers; null uses the manifest runtimes for the host operating system.</summary>
        public List<string>? Rids { get; set; }

        /// <summary>Tag to verify with the <c>verify-release</c> channel.</summary>
        public string? Tag { get; set; }

        /// <summary>Release notes destination for the <c>notes</c> channel.</summary>
        public string? NotesOutput { get; set; }

        /// <summary>True when help was requested.</summary>
        public bool Help { get; set; }

        /// <summary>Parses arguments.</summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>The options.</returns>
        /// <exception cref="PublisherException">An option is unknown or missing its value.</exception>
        public static PublisherOptions Parse(IReadOnlyList<string> args)
        {
            ArgumentNullException.ThrowIfNull(args);
            PublisherOptions options = new PublisherOptions();
            for (int index = 0; index < args.Count; index++)
            {
                string argument = args[index];
                switch (argument)
                {
                    case "-h": case "--help": options.Help = true; break;
                    case "--all": options.All = true; break;
                    case "--channel": options.Channels.AddRange(Split(Value(args, ref index, argument))); break;
                    case "--version": options.Version = Value(args, ref index, argument); break;
                    case "--root": options.Root = Value(args, ref index, argument); break;
                    case "--output": options.Output = Value(args, ref index, argument); break;
                    case "--rids": options.Rids = Split(Value(args, ref index, argument)).ToList(); break;
                    case "--tag": options.Tag = Value(args, ref index, argument); break;
                    case "--notes-output": options.NotesOutput = Value(args, ref index, argument); break;
                    default: throw new PublisherException("Unknown option '" + argument + "'. Run with --help.");
                }
            }
            if (!options.Help && !options.All && options.Channels.Count == 0) throw new PublisherException("Choose --channel <name> or --all. Run with --help.");
            return options;
        }

        private static string Value(IReadOnlyList<string> args, ref int index, string name)
        {
            if (index + 1 >= args.Count || args[index + 1].StartsWith("--", StringComparison.Ordinal)) throw new PublisherException(name + " needs a value.");
            index++;
            return args[index];
        }

        private static IEnumerable<string> Split(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
