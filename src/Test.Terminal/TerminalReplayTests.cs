namespace Test.Terminal
{
    using System.Text;
    using Xunit;

    public class TerminalReplayTests
    {
        [Fact]
        public void SplitUtf8SequenceIsDecodedAcrossChunks()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("A\u2500B");
            TerminalSnapshot snapshot = TerminalReplay.Replay(
                10,
                4,
                new[] { bytes[..2], bytes[2..] });

            Assert.StartsWith("A\u2500B", snapshot.VisibleRows[0].Text);
        }

        [Fact]
        public void CarriageReturnRedrawDoesNotInjectEraseSequences()
        {
            TerminalSnapshot snapshot = TerminalReplay.Replay(
                20,
                4,
                new[] { Encoding.UTF8.GetBytes("Downloading 10%\rDownloading 90%") });

            Assert.Equal("Downloading 90%", snapshot.VisibleRows[0].Text);
        }

        [Fact]
        public void CursorAddressedRewriteReplacesExistingRow()
        {
            TerminalSnapshot snapshot = TerminalReplay.Replay(
                20,
                4,
                new[] { Encoding.UTF8.GetBytes("first\nsecond\u001b[1;1Hupdated") },
                convertEol: true);

            Assert.StartsWith("updated", snapshot.VisibleRows[0].Text);
        }

        [Fact]
        public void WindowsConPtyProgressRewriteDoesNotRequireConvertEol()
        {
            string output =
                "[+] up 2/2\r\n" +
                " - Container alpha Waiting 1.0s\r\n" +
                " - Container beta Waiting 1.0s\r\n" +
                "\u001b[3A" +
                "\r\u001b[2K[+] up 2/2\r\n" +
                "\r\u001b[2K \u2714 Container alpha Healthy 2.0s\r\n" +
                "\r\u001b[2K \u2714 Container beta Healthy 2.0s";

            TerminalSnapshot snapshot = TerminalReplay.Replay(
                80,
                8,
                new[] { Encoding.UTF8.GetBytes(output) });

            Assert.Equal("[+] up 2/2", snapshot.VisibleRows[0].Text);
            Assert.Contains("Container alpha Healthy 2.0s", snapshot.VisibleRows[1].Text);
            Assert.Contains("Container beta Healthy 2.0s", snapshot.VisibleRows[2].Text);
            Assert.DoesNotContain("Waiting", snapshot.VisibleRows[1].Text);
            Assert.DoesNotContain("Waiting", snapshot.VisibleRows[2].Text);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FullWidthProgressRowsDoNotCreateExtraEntries(bool convertEol)
        {
            string row1 = "\u2714 Container docker-partio-server-1    Healthy".PadRight(80);
            string row2 = "\u2714 Container docker-litegraph-1        Healthy".PadRight(80);
            string output = row1 + "\r\n" + row2 + "\r\n";

            TerminalSnapshot snapshot = TerminalReplay.Replay(
                80,
                6,
                new[] { Encoding.UTF8.GetBytes(output) },
                convertEol,
                trimLineEndingPadding: true);

            Assert.Contains("docker-partio-server-1", snapshot.VisibleRows[0].Text);
            Assert.Contains("docker-litegraph-1", snapshot.VisibleRows[1].Text);
            Assert.DoesNotContain("docker-partio-server-1", snapshot.VisibleRows[1].Text);
            Assert.DoesNotContain("docker-litegraph-1", snapshot.VisibleRows[2].Text);
        }

        [Fact]
        public void SplitLineEndingPaddingDoesNotCreateExtraEntries()
        {
            string row1 = "\u2714 Container docker-documentatom-ui-1  Created".PadRight(80);
            string row2 = "\u2714 Container docker-documentatom-mcp-1 Created".PadRight(80);

            TerminalSnapshot snapshot = TerminalReplay.Replay(
                80,
                6,
                new[]
                {
                    Encoding.UTF8.GetBytes(row1),
                    Encoding.UTF8.GetBytes("\r\n"),
                    Encoding.UTF8.GetBytes(row2),
                    Encoding.UTF8.GetBytes("\r\n")
                },
                trimLineEndingPadding: true);

            Assert.Contains("docker-documentatom-ui-1", snapshot.VisibleRows[0].Text);
            Assert.Contains("docker-documentatom-mcp-1", snapshot.VisibleRows[1].Text);
            Assert.DoesNotContain("docker-documentatom-ui-1", snapshot.VisibleRows[1].Text);
            Assert.DoesNotContain("docker-documentatom-mcp-1", snapshot.VisibleRows[2].Text);
        }

        [Fact]
        public void DockerCursorPaddingDoesNotCreateExtraEntries()
        {
            string output =
                "[+] up 2/2\r\n" +
                " \u001b[32m\u2714 \u001b[mContainer docker-documentatom-ui-1  \u001b[32mCreated\u001b[130X\u001b[34m\u001b[130C0.3s \u001b[m\r\n" +
                " \u001b[32m\u2714 \u001b[mContainer docker-documentatom-mcp-1 \u001b[32mCreated\u001b[130X\u001b[34m\u001b[130C0.3s \u001b[m\r\n";

            TerminalSnapshot snapshot = TerminalReplay.Replay(180, 8, new[] { Encoding.UTF8.GetBytes(output) }, trimLineEndingPadding: true);

            Assert.Contains("docker-documentatom-ui-1", snapshot.VisibleRows[1].Text);
            Assert.Contains("docker-documentatom-mcp-1", snapshot.VisibleRows[2].Text);
            Assert.DoesNotContain("docker-documentatom-ui-1", snapshot.VisibleRows[2].Text);
            Assert.DoesNotContain("docker-documentatom-mcp-1", snapshot.VisibleRows[3].Text);
        }

        [Fact]
        public void DockerNetworkRowKeepsStatusColumnSpacing()
        {
            string output =
                " \u001b[32m\u2714 \u001b[mNetwork docker_default\u001b[14X\u001b[32m\u001b[14CCreated\u001b[130X\u001b[34m\u001b[130C0.1s \u001b[m\r\n";

            TerminalSnapshot snapshot = TerminalReplay.Replay(180, 4, new[] { Encoding.UTF8.GetBytes(output) }, trimLineEndingPadding: true);

            Assert.Contains("Network docker_default              Created", snapshot.VisibleRows[0].Text);
        }
    }
}
