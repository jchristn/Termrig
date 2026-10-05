namespace Test.Terminal
{
    using XTerm;
    using XTerm.Options;
    using Xunit;

    /// <summary>
    /// Guards against private-prefixed CSI sequences (as sent by Claude Code and other modern TUIs)
    /// being misinterpreted as their unprefixed counterparts.
    /// </summary>
    public class PrivateCsiSequenceTests
    {
        private static Terminal CreateTerminal()
        {
            var terminal = new Terminal(new TerminalOptions { Scrollback = 100 });
            terminal.Resize(40, 5);
            return terminal;
        }

        private static bool IsUnderlined(Terminal terminal, int x)
        {
            var line = terminal.Buffer.GetLine(terminal.Buffer.YBase + terminal.Buffer.Y);
            Assert.NotNull(line);
            return line![x].Attributes.IsUnderline();
        }

        [Theory]
        [InlineData("\u001b[>4mX")]          // XTMODKEYS (modifyOtherKeys)
        [InlineData("\u001b[>4;2mX")]
        [InlineData("\u001b[?4mX")]          // XTQMODKEYS
        [InlineData("\u001b[4m\u001b[4:0mX")] // colon-form underline off
        [InlineData("\u001b[58;5;4mX")]       // underline color (256)
        [InlineData("\u001b[58;2;4;4;4mX")]   // underline color (RGB)
        [InlineData("\u001b[58:2::4:4:4mX")]  // underline color (colon RGB)
        public void SequencesDoNotEnableUnderline(string input)
        {
            Terminal terminal = CreateTerminal();
            terminal.Write(input);
            Assert.False(IsUnderlined(terminal, 0));
        }

        [Theory]
        [InlineData("\u001b[4mX")]
        [InlineData("\u001b[4:1mX")]
        [InlineData("\u001b[4:3mX")]
        [InlineData("\u001b[58;5;1;4mX")]
        public void UnderlineSequencesEnableUnderline(string input)
        {
            Terminal terminal = CreateTerminal();
            terminal.Write(input);
            Assert.True(IsUnderlined(terminal, 0));
        }

        [Theory]
        [InlineData("\u001b[?u")]   // kitty keyboard query
        [InlineData("\u001b[>1u")]  // kitty keyboard push
        [InlineData("\u001b[>0q")]  // XTVERSION
        [InlineData("\u001b[?s")]   // XTSAVE
        public void PrivateSequencesDoNotMoveCursor(string input)
        {
            Terminal terminal = CreateTerminal();
            terminal.Write("\u001b[2;3H\u001b[s\u001b[4;10H" + input);
            Assert.Equal(9, terminal.Buffer.X);
            Assert.Equal(3, terminal.Buffer.Y);
        }
    }
}
