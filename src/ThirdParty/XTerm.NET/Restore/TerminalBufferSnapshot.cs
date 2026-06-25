using XTerm.Buffer;

namespace XTerm.Restore;

/// <summary>
/// Versioned snapshot of terminal buffer lines used to restore recent visual history.
/// </summary>
public sealed class TerminalBufferSnapshot
{
    public int SchemaVersion { get; set; } = 1;

    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public int Columns { get; set; }

    public int Rows { get; set; }

    public int CursorColumn { get; set; }

    public int CursorRow { get; set; }

    public int ViewportY { get; set; }

    public int BaseY { get; set; }

    public bool WasAlternateBufferActive { get; set; }

    public List<TerminalBufferLineSnapshot> Lines { get; set; } = new List<TerminalBufferLineSnapshot>();
}

/// <summary>
/// Snapshot of one terminal buffer line.
/// </summary>
public sealed class TerminalBufferLineSnapshot
{
    public bool IsWrapped { get; set; }

    public LineAttribute LineAttribute { get; set; } = LineAttribute.Normal;

    public List<TerminalBufferCellSnapshot> Cells { get; set; } = new List<TerminalBufferCellSnapshot>();
}

/// <summary>
/// Snapshot of one terminal buffer cell.
/// </summary>
public sealed class TerminalBufferCellSnapshot
{
    public string Text { get; set; } = String.Empty;

    public int Width { get; set; }

    public int CodePoint { get; set; }

    public int Foreground { get; set; }

    public int Background { get; set; }

    public int Extended { get; set; }
}
