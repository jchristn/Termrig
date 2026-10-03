namespace Test.Terminal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Porta.Pty;
    using XTerm;
    using XTerm.Options;
    using Xunit;

    /// <summary>
    /// Guards the behavior Termrig relies on from third-party packages whose major versions changed
    /// (Wcwidth 4.x for cell-width calculation, Porta.Pty 2.x for process spawning).
    /// </summary>
    public class DependencyCompatibilityTests
    {
        private static Terminal CreateTerminal()
        {
            var terminal = new Terminal(new TerminalOptions { Scrollback = 100 });
            terminal.Resize(40, 5);
            return terminal;
        }

        [Theory]
        [InlineData("a", 1)]
        [InlineData("abc", 3)]
        [InlineData("中", 2)]
        [InlineData("中文", 4)]
        [InlineData("é", 1)]
        [InlineData("é", 1)]
        public void CursorAdvancesByUnicodeCellWidth(string text, int expectedColumns)
        {
            Terminal terminal = CreateTerminal();

            terminal.Write(text);

            Assert.Equal(expectedColumns, terminal.Buffer.X);
            Assert.Equal(0, terminal.Buffer.Y);
        }

        [Fact]
        public void WideCharacterOccupiesTwoCellsWithSpacerCell()
        {
            Terminal terminal = CreateTerminal();

            terminal.Write("中x");

            var line = terminal.Buffer.GetLine(terminal.Buffer.YBase);
            Assert.NotNull(line);
            Assert.Equal("中", line![0].Content);
            Assert.Equal(2, line[0].Width);
            Assert.Equal(0, line[1].Width);
            Assert.Equal("x", line[2].Content);
            Assert.Equal(3, terminal.Buffer.X);
        }

        [Fact]
        public async Task PtyProviderSpawnsProcessAndStreamsOutput()
        {
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string app = isWindows ? Path.Combine(Environment.SystemDirectory, "cmd.exe") : "/bin/sh";
            string[] args = isWindows ? new[] { "/c", "echo termrig-pty-ok" } : new[] { "-c", "echo termrig-pty-ok" };

            var options = new PtyOptions
            {
                Name = "termrig-test",
                Cols = 80,
                Rows = 24,
                Cwd = Environment.CurrentDirectory,
                App = app,
                CommandLine = args,
                Environment = new Dictionary<string, string>()
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            IPtyConnection connection = await PtyProvider.SpawnAsync(options, cts.Token);
            try
            {
                var output = new StringBuilder();
                byte[] buffer = new byte[4096];
                Task readLoop = Task.Run(() =>
                {
                    try
                    {
                        int read;
                        while ((read = connection.ReaderStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            lock (output)
                                output.Append(Encoding.UTF8.GetString(buffer, 0, read));
                            lock (output)
                                if (output.ToString().Contains("termrig-pty-ok", StringComparison.Ordinal))
                                    return;
                        }
                    }
                    catch (IOException)
                    {
                        // Reader closes when the child exits.
                    }
                });

                await Task.WhenAny(readLoop, Task.Delay(TimeSpan.FromSeconds(20)));

                lock (output)
                    Assert.Contains("termrig-pty-ok", output.ToString(), StringComparison.Ordinal);

                Assert.True(connection.WaitForExit(10000));
                Assert.Equal(0, connection.ExitCode);
            }
            finally
            {
                connection.Dispose();
            }
        }
    }
}
