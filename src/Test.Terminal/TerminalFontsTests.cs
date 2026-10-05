namespace Test.Terminal
{
    using System;
    using Termrig.App.Services;
    using Xunit;

    /// <summary>
    /// Verifies terminal font resolution prefers bundled fonts and always appends symbol fallbacks.
    /// </summary>
    public class TerminalFontsTests
    {
        [Theory]
        [InlineData("Cascadia Code", "avares://Termrig/Assets/Fonts#Cascadia Code")]
        [InlineData("cascadia mono", "avares://Termrig/Assets/Fonts#Cascadia Mono")]
        [InlineData("Cascadia Code, Consolas", "avares://Termrig/Assets/Fonts#Cascadia Code")]
        public void BundledFamiliesResolveToEmbeddedFonts(string requested, string expectedPrimary)
        {
            string list = TerminalFonts.BuildFamilyList(requested);
            Assert.StartsWith(expectedPrimary + ",", list, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Termrig Missing Font 12345")]
        public void MissingFamiliesFallBackToBundledDefault(string? requested)
        {
            string list = TerminalFonts.BuildFamilyList(requested);
            Assert.StartsWith("avares://Termrig/Assets/Fonts#" + TerminalFonts.DefaultFamilyName + ",", list, StringComparison.Ordinal);
        }

        [Fact]
        public void SymbolFallbacksAreAppended()
        {
            string list = TerminalFonts.BuildFamilyList("Cascadia Code");
            Assert.Contains("avares://Termrig/Assets/Fonts#Noto Sans Symbols 2", list, StringComparison.Ordinal);
            Assert.Contains("avares://Termrig/Assets/Fonts#Noto Sans Symbols", list, StringComparison.Ordinal);
        }
    }
}
