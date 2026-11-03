namespace Test.Terminal
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using XTerm;
    using XTerm.Options;
    using Xunit;

    public class TerminalTextFidelityTests
    {
        public static IEnumerable<object[]> JsonCases()
        {
            foreach (int columns in new[] { 20, 80, 240 })
            foreach (int rows in new[] { 4, 24 })
            // CRLF is the usual PTY output on Windows and on Unix with ONLCR.
            // LF with ConvertEol covers Unix PTYs without ONLCR.
            foreach (var ending in new[] { ("\r\n", false), ("\r\n", true), ("\n", true) })
            foreach (int chunkSize in new[] { 1, 4096 })
                yield return new object[] { columns, rows, ending.Item1, ending.Item2, chunkSize };
        }

        [Theory]
        [MemberData(nameof(JsonCases))]
        public void CopiedJsonPreservesLogicalLinesAcrossWrapsAndScrollback(
            int columns, int rows, string lineEnding, bool convertEol, int chunkSize)
        {
            string json = "{\n  \"token\": \"" + new string('A', 170) +
                "\",\n\n  \"next\": \"" + new string('B', 90) + "\",\n  \"ok\": true\n}";
            var terminal = new Terminal(new TerminalOptions
            {
                Cols = columns, Rows = rows, ConvertEol = convertEol, Scrollback = 100
            });

            try
            {
                WriteChunks(terminal, json.Replace("\n", lineEnding), chunkSize);
                terminal.Buffer.ScrollToBottom();
                int startY = -terminal.Buffer.YDisp;
                int endY = terminal.Buffer.YBase + terminal.Buffer.Y - terminal.Buffer.YDisp;
                int endX = terminal.Buffer.X - 1;

                // Both drag directions must reconstruct the same logical text.
                foreach (bool reverse in new[] { false, true })
                {
                    terminal.Selection.StartSelection(reverse ? endX : 0, reverse ? endY : startY);
                    terminal.Selection.UpdateSelection(reverse ? 0 : endX, reverse ? startY : endY);
                    terminal.Selection.EndSelection();

                    string copied = terminal.Selection.GetSelectionText();
                    Assert.Equal(json, copied);
                    using JsonDocument document = JsonDocument.Parse(copied);
                    Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
                }
            }
            finally
            {
                terminal.Dispose();
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PartialSelectionPreservesSpacesAtSoftWrap(bool trimWhitespace)
        {
            var terminal = new Terminal(new TerminalOptions
            {
                Cols = 8, Rows = 4, TrimSelectionTrailingWhitespace = trimWhitespace
            });
            try
            {
                terminal.Write("abc     def\r\nnext");
                terminal.Selection.StartSelection(1, 0);
                terminal.Selection.UpdateSelection(2, 1);
                terminal.Selection.EndSelection();
                Assert.Equal("bc     def", terminal.Selection.GetSelectionText());
            }
            finally
            {
                terminal.Dispose();
            }
        }

        [Theory]
        [InlineData(false, false, 1, 3)]
        [InlineData(false, true, 4096, 24)]
        [InlineData(true, false, 4096, 3)]
        [InlineData(true, true, 1, 24)]
        public void ZshPromptPreservesOutputWithOrWithoutFinalNewline(
            bool finalNewline, bool convertEol, int chunkSize, int rows)
        {
            var terminal = new Terminal(new TerminalOptions
            {
                Cols = 40, Rows = rows, ConvertEol = convertEol, Scrollback = 100
            });
            try
            {
                string output = "{\r\n  \"ok\": true\r\n}" + (finalNewline ? "\r\n" : "");
                // Captured from macOS zsh -f at 40 columns after printf without a
                // trailing newline. PROMPT_SP writes a marker and 39 spaces to
                // advance past a partial line, then clears the new prompt row.
                output += "\u001b[1m\u001b[7m%\u001b[27m\u001b[1m\u001b[0m" +
                    new string(' ', 39) + "\r \r\r\u001b[0m\u001b[27m\u001b[24m" +
                    "\u001b[JPROMPT> \u001b[K\u001b[?2004h";

                WriteChunks(terminal, output, chunkSize);

                Assert.Equal("{", terminal.Buffer.Lines[0]!.TranslateToString(true));
                Assert.Equal("  \"ok\": true", terminal.Buffer.Lines[1]!.TranslateToString(true));
                Assert.Equal(finalNewline ? "}" : "}%", terminal.Buffer.Lines[2]!.TranslateToString(true));
                Assert.Equal("PROMPT>", terminal.Buffer.Lines[3]!.TranslateToString(true));
                Assert.Equal(3, terminal.Buffer.YBase + terminal.Buffer.Y);
            }
            finally
            {
                terminal.Dispose();
            }
        }

        private static void WriteChunks(Terminal terminal, string output, int chunkSize)
        {
            for (int offset = 0; offset < output.Length; offset += chunkSize)
                terminal.Write(output.Substring(offset, Math.Min(chunkSize, output.Length - offset)));
        }
    }
}
