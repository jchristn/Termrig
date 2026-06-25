namespace Test.Terminal
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Termrig.App.Services;
    using Termrig.Core.Models;
    using XTerm;
    using XTerm.Options;
    using XTerm.Restore;
    using Xunit;

    public class TerminalRestoreTests
    {
        [Fact]
        public void BufferSnapshotRoundTripRestoresTextAndAttributes()
        {
            var terminal = new Terminal(new TerminalOptions { Scrollback = 100 });
            terminal.Resize(20, 5);
            terminal.Write("plain\r\n");
            terminal.Write("\u001b[31mred\u001b[0m\r\n");

            TerminalBufferSnapshot snapshot = terminal.ExportBufferSnapshot(10);
            var restored = new Terminal(new TerminalOptions { Scrollback = 100 });
            restored.Resize(20, 5);
            restored.RestoreBufferSnapshot(snapshot);

            string[] restoredLines = restored.Buffer.Lines.GetItems()
                .Select(line => line.TranslateToString(true))
                .ToArray();

            Assert.Contains("plain", restoredLines);
            Assert.Contains("red", restoredLines);
            TerminalBufferCellSnapshot redCell = snapshot.Lines
                .SelectMany(line => line.Cells)
                .First(cell => cell.Text == "r");
            Assert.NotEqual(default, redCell.Foreground);
        }

        [Fact]
        public void BufferSnapshotHonorsLineLimit()
        {
            var terminal = new Terminal(new TerminalOptions { Scrollback = 100 });
            terminal.Resize(20, 3);
            for (int index = 0; index < 10; index++)
            {
                terminal.Write("line-" + index + "\r\n");
            }

            TerminalBufferSnapshot snapshot = terminal.ExportBufferSnapshot(4);
            string snapshotText = String.Join(
                "\n",
                snapshot.Lines.Select(line => String.Concat(line.Cells.Select(cell => cell.Text))));

            Assert.Equal(4, snapshot.Lines.Count);
            Assert.Contains("line-9", snapshotText);
            Assert.DoesNotContain("line-0", snapshotText);
        }

        [Fact]
        public void BufferSnapshotRecordsAlternateBufferButExportsNormalBuffer()
        {
            var terminal = new Terminal(new TerminalOptions { Scrollback = 100 });
            terminal.Resize(20, 5);
            terminal.Write("normal\r\n");
            terminal.Write("\u001b[?1049halt\r\n");

            TerminalBufferSnapshot snapshot = terminal.ExportBufferSnapshot(10);
            string snapshotText = String.Concat(snapshot.Lines.SelectMany(line => line.Cells.Select(cell => cell.Text)));

            Assert.True(snapshot.WasAlternateBufferActive);
            Assert.Contains("normal", snapshotText);
            Assert.DoesNotContain("alt", snapshotText);
        }

        [Fact]
        public async Task TerminalRestoreStoreSavesLoadsDeletesAndIgnoresCorruptFiles()
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-restore-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new TerminalRestoreStore(directory);
                var profile = new TerminalProfile { Id = "profile1", Name = "Work" };
                var tab = new TerminalTabProfile { Id = "tab1", Name = "Shell" };
                var terminal = new Terminal(new TerminalOptions { Scrollback = 100 });
                terminal.Resize(20, 5);
                terminal.Write("saved\r\n");
                TerminalBufferSnapshot snapshot = terminal.ExportBufferSnapshot(10);

                await store.SaveAsync(profile, tab, snapshot, 10, @"C:\Work");
                TerminalBufferSnapshot? loaded = await store.LoadAsync(profile, tab);

                Assert.NotNull(loaded);
                Assert.Contains("saved", String.Concat(loaded!.Lines.SelectMany(line => line.Cells.Select(cell => cell.Text))));

                string path = Path.Combine(directory, "profile1", "tab1.json");
                await File.WriteAllTextAsync(path, "{bad json");
                Assert.Null(await store.LoadAsync(profile, tab));

                await store.DeleteAsync(profile, tab);
                Assert.False(File.Exists(path));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
