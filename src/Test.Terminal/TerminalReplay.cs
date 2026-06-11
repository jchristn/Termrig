namespace Test.Terminal
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using XTerm;
    using XTerm.Buffer;
    using XTerm.Events;
    using XTerm.Options;

    internal sealed record TerminalCellSnapshot(string Text, int Width, string Attributes);

    internal sealed record TerminalRowSnapshot(string Text, IReadOnlyList<TerminalCellSnapshot> Cells);

    internal sealed record TerminalSnapshot(
        int Columns,
        int Rows,
        int CursorColumn,
        int CursorRow,
        int ViewportY,
        int YBase,
        bool IsAlternateBuffer,
        IReadOnlyList<TerminalRowSnapshot> VisibleRows);

    internal sealed record TerminalReplayOptions(
        int Columns,
        int Rows,
        bool ConvertEol = false,
        bool TrimLineEndingPadding = false,
        int Scrollback = 1000,
        string TermName = "xterm-256color",
        bool PinViewportToBottomAfterWrite = false);

    internal static class TerminalReplay
    {
        public static TerminalSnapshot Replay(
            int columns,
            int rows,
            IReadOnlyList<byte[]> chunks,
            bool convertEol = false,
            bool trimLineEndingPadding = false)
        {
            return Replay(
                new TerminalReplayOptions(
                    Columns: columns,
                    Rows: rows,
                    ConvertEol: convertEol,
                    TrimLineEndingPadding: trimLineEndingPadding),
                chunks);
        }

        public static TerminalSnapshot Replay(TerminalReplayOptions options, IReadOnlyList<byte[]> chunks)
        {
            bool isAlternateBuffer = false;
            var terminal = new Terminal(new TerminalOptions
            {
                ConvertEol = options.ConvertEol,
                Scrollback = options.Scrollback,
                TermName = options.TermName
            });
            terminal.BufferChanged += (_, e) => isAlternateBuffer = e.Buffer == XTerm.Common.BufferType.Alternate;
            terminal.Resize(options.Columns, options.Rows);

            Decoder decoder = Encoding.UTF8.GetDecoder();
            var lineEndingPaddingNormalizer = new LineEndingPaddingNormalizer();
            char[] charBuffer = new char[Encoding.UTF8.GetMaxCharCount(4096)];
            foreach (byte[] chunk in chunks)
            {
                if (chunk.Length == 0)
                    continue;

                if (chunk.Length > 4096)
                    charBuffer = new char[Encoding.UTF8.GetMaxCharCount(chunk.Length)];

                int chars = decoder.GetChars(chunk, 0, chunk.Length, charBuffer, 0, flush: false);
                if (chars > 0)
                {
                    string text = new string(charBuffer, 0, chars);
                    terminal.Write(options.TrimLineEndingPadding ? lineEndingPaddingNormalizer.Process(text, options.Columns) : text);
                    ScrollToBottomIfRequested(terminal, isAlternateBuffer, options);
                }
            }

            int trailingChars = decoder.GetChars(Array.Empty<byte>(), 0, 0, charBuffer, 0, flush: true);
            if (trailingChars > 0)
            {
                string text = new string(charBuffer, 0, trailingChars);
                terminal.Write(options.TrimLineEndingPadding ? lineEndingPaddingNormalizer.Process(text, options.Columns) : text);
                ScrollToBottomIfRequested(terminal, isAlternateBuffer, options);
            }

            if (options.TrimLineEndingPadding)
            {
                string pendingText = lineEndingPaddingNormalizer.Flush();
                if (pendingText.Length > 0)
                {
                    terminal.Write(pendingText);
                    ScrollToBottomIfRequested(terminal, isAlternateBuffer, options);
                }
            }

            return Capture(terminal, isAlternateBuffer);
        }

        private static void ScrollToBottomIfRequested(Terminal terminal, bool isAlternateBuffer, TerminalReplayOptions options)
        {
            if (!options.PinViewportToBottomAfterWrite)
                return;

            if (!isAlternateBuffer || terminal.Buffer.ViewportY != terminal.Buffer.BaseY)
            {
                terminal.Buffer.ScrollToBottom();
            }
        }

        private static TerminalSnapshot Capture(Terminal terminal, bool isAlternateBuffer)
        {
            var rows = new List<TerminalRowSnapshot>();
            int startLine = terminal.Buffer.ViewportY;
            int endLine = Math.Min(terminal.Buffer.Length, startLine + terminal.Rows);
            for (int y = startLine; y < endLine; y++)
            {
                BufferLine? line = terminal.Buffer.GetLine(y);
                if (line == null)
                    continue;

                var cells = new List<TerminalCellSnapshot>();
                var text = new StringBuilder();
                int limit = Math.Min(line.Length, terminal.Cols);
                for (int x = 0; x < limit; x++)
                {
                    BufferCell cell = line[x];
                    string content = cell.Content ?? string.Empty;
                    text.Append(content.Length == 0 ? " " : content);
                    cells.Add(new TerminalCellSnapshot(content, cell.Width, cell.Attributes.ToString() ?? string.Empty));
                }

                rows.Add(new TerminalRowSnapshot(text.ToString().TrimEnd(), cells));
            }

            return new TerminalSnapshot(
                terminal.Cols,
                terminal.Rows,
                terminal.Buffer.X,
                terminal.Buffer.Y,
                terminal.Buffer.ViewportY,
                terminal.Buffer.YBase,
                isAlternateBuffer,
                rows);
        }

        private sealed class LineEndingPaddingNormalizer
        {
            private int _pendingSpaces;

            private int _column;

            public string Process(string output, int columns)
            {
                if (output.Length == 0)
                    return output;

                var builder = new StringBuilder(output.Length + _pendingSpaces);
                for (int index = 0; index < output.Length; index++)
                {
                    char ch = output[index];
                    if (ch == ' ')
                    {
                        _pendingSpaces++;
                        continue;
                    }

                    if (ch == '\r' || ch == '\n')
                    {
                        _pendingSpaces = 0;
                        builder.Append(ch);
                        if (ch == '\r')
                        {
                            _column = 0;
                        }
                        continue;
                    }

                    if (ch == '\u001b' && index + 1 < output.Length && output[index + 1] == '[')
                    {
                        int finalIndex = index + 2;
                        while (finalIndex < output.Length && (output[finalIndex] < '@' || output[finalIndex] > '~'))
                        {
                            finalIndex++;
                        }

                        if (finalIndex < output.Length)
                        {
                            string parameters = output.Substring(index + 2, finalIndex - index - 2);
                            char final = output[finalIndex];
                            if (final == 'm')
                            {
                                builder.Append(output, index, finalIndex - index + 1);
                                index = finalIndex;
                                continue;
                            }

                            FlushTo(builder);
                            if (final == 'C' && IsPaddingCount(parameters, columns))
                            {
                                int count = ParseCsiCount(parameters, 1);
                                int targetColumn = Math.Min(Math.Max(0, columns - 1), _column + count);
                                builder.Append("\u001b[");
                                builder.Append(targetColumn + 1);
                                builder.Append('G');
                                _column = targetColumn;
                                index = finalIndex;
                                continue;
                            }
                            else if (final == 'X' && IsPaddingCount(parameters, columns))
                            {
                                builder.Append("\u001b[K");
                                index = finalIndex;
                                continue;
                            }

                            builder.Append(output, index, finalIndex - index + 1);
                            TrackCsiPosition(parameters, final);
                            index = finalIndex;
                            continue;
                        }
                    }

                    FlushTo(builder);
                    builder.Append(ch);
                    if (!char.IsControl(ch))
                    {
                        _column = Math.Min(Math.Max(0, columns - 1), _column + 1);
                    }
                }

                return builder.ToString();
            }

            public string Flush()
            {
                if (_pendingSpaces == 0)
                    return string.Empty;

                string output = new string(' ', _pendingSpaces);
                _pendingSpaces = 0;
                return output;
            }

            private void FlushTo(StringBuilder builder)
            {
                if (_pendingSpaces == 0)
                    return;

                builder.Append(' ', _pendingSpaces);
                _column += _pendingSpaces;
                _pendingSpaces = 0;
            }

            private void TrackCsiPosition(string parameters, char final)
            {
                if (final == 'G')
                {
                    _column = Math.Max(0, ParseCsiCount(parameters, 1) - 1);
                }
                else if (final == 'C')
                {
                    _column += ParseCsiCount(parameters, 1);
                }
                else if (final == 'D')
                {
                    _column = Math.Max(0, _column - ParseCsiCount(parameters, 1));
                }
                else if (final == 'H' || final == 'f')
                {
                    string[] parts = parameters.Split(';');
                    if (parts.Length >= 2 && int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int column))
                    {
                        _column = Math.Max(0, column - 1);
                    }
                    else
                    {
                        _column = 0;
                    }
                }

                _column = Math.Min(Math.Max(0, _column), Int32.MaxValue);
            }

            private static int ParseCsiCount(string parameters, int defaultValue)
            {
                if (string.IsNullOrWhiteSpace(parameters))
                    return defaultValue;

                string first = parameters.Split(';')[0];
                return int.TryParse(first.TrimStart('?'), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value)
                    ? Math.Max(1, value)
                    : defaultValue;
            }

            private static bool IsPaddingCount(string parameters, int columns)
            {
                if (columns <= 0)
                    return false;

                return ParseCsiCount(parameters, 1) > columns / 2;
            }
        }
    }
}
