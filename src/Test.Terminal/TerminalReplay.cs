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

    internal static class TerminalReplay
    {
        public static TerminalSnapshot Replay(
            int columns,
            int rows,
            IReadOnlyList<byte[]> chunks,
            bool convertEol = false,
            bool trimLineEndingPadding = false)
        {
            bool isAlternateBuffer = false;
            var terminal = new Terminal(new TerminalOptions
            {
                ConvertEol = convertEol,
                Scrollback = 1000,
                TermName = "xterm-256color"
            });
            terminal.BufferChanged += (_, e) => isAlternateBuffer = e.Buffer == XTerm.Common.BufferType.Alternate;
            terminal.Resize(columns, rows);

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
                    terminal.Write(trimLineEndingPadding ? lineEndingPaddingNormalizer.Process(text, columns) : text);
                }
            }

            int trailingChars = decoder.GetChars(Array.Empty<byte>(), 0, 0, charBuffer, 0, flush: true);
            if (trailingChars > 0)
            {
                string text = new string(charBuffer, 0, trailingChars);
                terminal.Write(trimLineEndingPadding ? lineEndingPaddingNormalizer.Process(text, columns) : text);
            }

            if (trimLineEndingPadding)
            {
                string pendingText = lineEndingPaddingNormalizer.Flush();
                if (pendingText.Length > 0)
                {
                    terminal.Write(pendingText);
                }
            }

            return Capture(terminal, isAlternateBuffer);
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

            private bool _originMode;

            private int _scrollTop;

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
                            if (TryNormalizeCsi(builder, parameters, final))
                            {
                                index = finalIndex;
                                continue;
                            }

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

            private bool TryNormalizeCsi(StringBuilder builder, string parameters, char final)
            {
                if (final == 'r')
                {
                    int top = Math.Max(ParseCsiPart(parameters, 0, 1), 1) - 1;
                    _scrollTop = Math.Max(0, top);
                    builder.Append("\u001b[");
                    builder.Append(parameters);
                    builder.Append(final);
                    AppendCursorPosition(builder, _originMode ? _scrollTop : 0, 0);
                    _column = 0;
                    return true;
                }

                if ((final == 'h' || final == 'l') && HasPrivateMode(parameters, 6))
                {
                    _originMode = final == 'h';
                    builder.Append("\u001b[");
                    builder.Append(parameters);
                    builder.Append(final);
                    AppendCursorPosition(builder, _originMode ? _scrollTop : 0, 0);
                    _column = 0;
                    return true;
                }

                if (_originMode && (final == 'H' || final == 'f'))
                {
                    int row = Math.Max(ParseCsiPart(parameters, 0, 1), 1) - 1;
                    int column = Math.Max(ParseCsiPart(parameters, 1, 1), 1) - 1;
                    AppendCursorPosition(builder, _scrollTop + row, column);
                    _column = column;
                    return true;
                }

                if (_originMode && final == 'd')
                {
                    int row = Math.Max(ParseCsiPart(parameters, 0, 1), 1) - 1;
                    builder.Append("\u001b[");
                    builder.Append(_scrollTop + row + 1);
                    builder.Append('d');
                    return true;
                }

                return false;
            }

            private static void AppendCursorPosition(StringBuilder builder, int row, int column)
            {
                builder.Append("\u001b[");
                builder.Append(row + 1);
                builder.Append(';');
                builder.Append(column + 1);
                builder.Append('H');
            }

            private static bool HasPrivateMode(string parameters, int mode)
            {
                if (!parameters.StartsWith("?", StringComparison.Ordinal))
                    return false;

                string[] parts = parameters[1..].Split(';');
                foreach (string part in parts)
                {
                    if (int.TryParse(part, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value) && value == mode)
                        return true;
                }

                return false;
            }

            private static int ParseCsiPart(string parameters, int index, int defaultValue)
            {
                if (string.IsNullOrWhiteSpace(parameters))
                    return defaultValue;

                string[] parts = parameters.TrimStart('?').Split(';');
                if (index >= parts.Length || string.IsNullOrWhiteSpace(parts[index]))
                    return defaultValue;

                return int.TryParse(parts[index], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int value)
                    ? value
                    : defaultValue;
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
