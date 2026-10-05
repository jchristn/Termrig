namespace XTerm.Common;

/// <summary>
/// Extension methods for parsing CSI command identifiers.
/// </summary>
public static class CsiCommandExtensions
{
    private static readonly Dictionary<string, CsiCommand> _commandMap = new()
    {
        { "@", CsiCommand.InsertChars },
        { "A", CsiCommand.CursorUp },
        { "B", CsiCommand.CursorDown },
        { "C", CsiCommand.CursorForward },
        { "D", CsiCommand.CursorBackward },
        { "E", CsiCommand.CursorNextLine },
        { "F", CsiCommand.CursorPreviousLine },
        { "G", CsiCommand.CursorCharAbsolute },
        { "H", CsiCommand.CursorPosition },
        { "I", CsiCommand.CursorForwardTab },
        { "J", CsiCommand.EraseInDisplay },
        { "K", CsiCommand.EraseInLine },
        { "L", CsiCommand.InsertLines },
        { "M", CsiCommand.DeleteLines },
        { "P", CsiCommand.DeleteChars },
        { "S", CsiCommand.ScrollUp },
        { "T", CsiCommand.ScrollDown },
        { "X", CsiCommand.EraseChars },
        { "Z", CsiCommand.CursorBackwardTab },
        { "c", CsiCommand.DeviceAttributes },
        { "d", CsiCommand.LinePositionAbsolute },
        { "f", CsiCommand.CursorPosition }, // HVP - same as CUP
        { "g", CsiCommand.TabClear },
        { "h", CsiCommand.SetMode },
        { "l", CsiCommand.ResetMode },
        { "m", CsiCommand.SelectGraphicRendition },
        { "n", CsiCommand.DeviceStatusReport },
        { "r", CsiCommand.SetScrollRegion },
        { "s", CsiCommand.SaveCursorAnsi },
        { "t", CsiCommand.WindowManipulation },
        { "u", CsiCommand.RestoreCursorAnsi },
        { " q", CsiCommand.SelectCursorStyle },
        { "q", CsiCommand.SelectCursorStyle }
    };

    private static readonly HashSet<CsiCommand> _privateCapableCommands = new()
    {
        CsiCommand.DeviceAttributes,    // CSI > c (secondary DA)
        CsiCommand.DeviceStatusReport,  // CSI ? n (DEC DSR)
        CsiCommand.SetMode,             // CSI ? h (DECSET)
        CsiCommand.ResetMode,           // CSI ? l (DECRST)
        CsiCommand.EraseInDisplay,      // CSI ? J (DECSED)
        CsiCommand.EraseInLine          // CSI ? K (DECSEL)
    };

    /// <summary>
    /// Converts a CSI identifier string to a CsiCommand enum value.
    /// </summary>
    /// <param name="identifier">The CSI identifier (final character, possibly with prefix)</param>
    /// <returns>The corresponding CsiCommand enum value, or Unknown if not recognized</returns>
    public static CsiCommand ToCsiCommand(this string identifier)
    {
        // Handle DEC private mode sequences (e.g., "?h", "?l", ">c")
        var cleaned = identifier.TrimStart('?', '>');
        var command = _commandMap.GetValueOrDefault(cleaned, CsiCommand.Unknown);

        // Only commands with a defined private form may carry a '?' or '>' prefix. Other prefixed
        // sequences are distinct commands (e.g. "CSI > 4 m" is XTMODKEYS, "CSI ? u" is a kitty
        // keyboard query, "CSI > 0 q" is XTVERSION) and must not be treated as SGR, DECRC, DECSCUSR, etc.
        if (cleaned.Length != identifier.Length && !_privateCapableCommands.Contains(command))
        {
            return CsiCommand.Unknown;
        }

        return command;
    }
    
    /// <summary>
    /// Checks if a CSI identifier represents a DEC private mode sequence.
    /// </summary>
    /// <param name="identifier">The CSI identifier</param>
    /// <returns>True if the identifier starts with '?' or '>', indicating a DEC private mode</returns>
    public static bool IsPrivateMode(this string identifier)
    {
        return identifier.StartsWith('?') || identifier.StartsWith('>');
    }
}

/// <summary>
/// Extension methods for working with OSC commands.
/// </summary>
public static class OscCommandExtensions
{
    /// <summary>
    /// Tries to parse an OSC command string to an OscCommand enum value.
    /// </summary>
    /// <param name="commandString">The command string (numeric identifier)</param>
    /// <param name="command">The parsed OscCommand enum value</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryParseOscCommand(this string commandString, out OscCommand command)
    {
        if (int.TryParse(commandString, out int commandValue) && 
            Enum.IsDefined(typeof(OscCommand), commandValue))
        {
            command = (OscCommand)commandValue;
            return true;
        }
        
        command = default;
        return false;
    }
}
