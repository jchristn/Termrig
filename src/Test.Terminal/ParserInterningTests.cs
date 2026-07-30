namespace Test.Terminal
{
    using System.Collections.Generic;
    using XTerm.Parser;
    using Xunit;

    /// <summary>
    /// Covers the ASCII glyph-string interning fast path in <see cref="EscapeSequenceParser"/> that
    /// removes a heap allocation per printed ASCII character.
    /// </summary>
    public class ParserInterningTests
    {
        private static List<string> CapturePrints(string input)
        {
            var parser = new EscapeSequenceParser();
            var captured = new List<string>();
            parser.Print += (_, e) => captured.Add(e.Data);
            parser.Parse(input);
            return captured;
        }

        [Fact]
        public void RepeatedAsciiCharactersReuseTheSameInternedString()
        {
            List<string> prints = CapturePrints("AA");

            Assert.Equal(2, prints.Count);
            Assert.Equal("A", prints[0]);
            Assert.Equal("A", prints[1]);
            // Positive: the two prints must be the exact same cached instance (no per-glyph allocation).
            Assert.Same(prints[0], prints[1]);
        }

        [Fact]
        public void AsciiInterningIsSharedAcrossParserInstances()
        {
            string first = CapturePrints("~")[0];
            string second = CapturePrints("~")[0];

            Assert.Equal("~", first);
            // The cache is process-wide, so independent parsers hand out the same instance.
            Assert.Same(first, second);
        }

        [Fact]
        public void PrintedCharactersAreContentCorrectAcrossTheAsciiBoundary()
        {
            // 0x7E '~' is the top of the interned printable-ASCII range; the surrounding content must
            // still be exactly what was written.
            List<string> prints = CapturePrints("a ~z");

            Assert.Equal(new[] { "a", " ", "~", "z" }, prints);
        }

        [Fact]
        public void NonAsciiCharactersAreStillDecodedCorrectly()
        {
            // Negative: code points above the ASCII cache take the ConvertFromUtf32 path. Correctness
            // is what matters here, not interning.
            List<string> prints = CapturePrints("é€");

            Assert.Equal(new[] { "é", "€" }, prints);
        }
    }
}
