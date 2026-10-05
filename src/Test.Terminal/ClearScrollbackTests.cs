namespace Test.Terminal
{
    using XTerm;
    using XTerm.Options;
    using Xunit;

    /// <summary>
    /// Verifies scrollback clearing (Clear History and ED 3) discards history but keeps the visible screen.
    /// </summary>
    public class ClearScrollbackTests
    {
        private static Terminal CreateTerminalWithHistory()
        {
            var terminal = new Terminal(new TerminalOptions { Scrollback = 100 });
            terminal.Resize(20, 3);
            for (int i = 0; i < 10; i++)
            {
                terminal.Write("line" + i + "\r\n");
            }

            terminal.Write("prompt");
            return terminal;
        }

        private static string RowText(Terminal terminal, int row)
        {
            var line = terminal.Buffer.GetLine(terminal.Buffer.YBase + row);
            Assert.NotNull(line);
            return line!.TranslateToString(true);
        }

        [Fact]
        public void ClearScrollbackKeepsVisibleScreen()
        {
            Terminal terminal = CreateTerminalWithHistory();
            Assert.True(terminal.Buffer.YBase > 0);
            string[] before = { RowText(terminal, 0), RowText(terminal, 1), RowText(terminal, 2) };
            int cursorX = terminal.Buffer.X;
            int cursorY = terminal.Buffer.Y;

            terminal.ClearScrollback();

            Assert.Equal(0, terminal.Buffer.YBase);
            Assert.Equal(0, terminal.Buffer.ViewportY);
            Assert.Equal(before, new[] { RowText(terminal, 0), RowText(terminal, 1), RowText(terminal, 2) });
            Assert.Equal(cursorX, terminal.Buffer.X);
            Assert.Equal(cursorY, terminal.Buffer.Y);
        }

        [Fact]
        public void EraseScrollbackSequenceKeepsVisibleScreen()
        {
            Terminal terminal = CreateTerminalWithHistory();
            string bottom = RowText(terminal, 2);

            terminal.Write("\u001b[3J");

            Assert.Equal(0, terminal.Buffer.YBase);
            Assert.Equal(bottom, RowText(terminal, 2));
            Assert.Equal("prompt", bottom);
        }
    }
}
