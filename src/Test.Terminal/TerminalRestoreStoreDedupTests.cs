namespace Test.Terminal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Termrig.App.Services;
    using Termrig.Core.Models;
    using XTerm.Restore;
    using Xunit;

    /// <summary>
    /// Covers the content-dedup guard in <see cref="TerminalRestoreStore.SaveAsync"/>, which skips the
    /// (potentially large) serialization + disk write when a terminal's buffer is unchanged since the
    /// last save — the common case for terminals producing repeated identical snapshots.
    /// </summary>
    public class TerminalRestoreStoreDedupTests
    {
        private static TerminalBufferSnapshot MakeBuffer(string firstLineText)
        {
            var cells = new List<TerminalBufferCellSnapshot>();
            foreach (char c in firstLineText)
            {
                cells.Add(new TerminalBufferCellSnapshot { Text = c.ToString(), Width = 1, CodePoint = c });
            }

            return new TerminalBufferSnapshot
            {
                Columns = 80,
                Rows = 24,
                Lines = new List<TerminalBufferLineSnapshot>
                {
                    new TerminalBufferLineSnapshot { Cells = cells }
                }
            };
        }

        private static (TerminalProfile Profile, TerminalTabProfile Tab) MakeIdentity()
        {
            var profile = new TerminalProfile { Name = "Profile" };
            profile.Id = "profile-fixed-id";
            var tab = new TerminalTabProfile { Name = "Tab" };
            tab.Id = "tab-fixed-id";
            return (profile, tab);
        }

        [Fact]
        public async Task IdenticalSnapshotIsSkippedButChangedSnapshotIsWritten()
        {
            string dir = Path.Combine(Path.GetTempPath(), "termrig-restore-" + Guid.NewGuid().ToString("N"));
            var store = new TerminalRestoreStore(dir);
            (TerminalProfile profile, TerminalTabProfile tab) = MakeIdentity();

            try
            {
                // First save writes.
                bool first = await store.SaveAsync(profile, tab, MakeBuffer("hello"), 100);
                Assert.True(first);

                // Positive (dedup): a fresh snapshot object with identical content is skipped even
                // though its capture timestamp differs.
                bool second = await store.SaveAsync(profile, tab, MakeBuffer("hello"), 100);
                Assert.False(second);

                // Negative (must write): changed content is not deduped.
                bool third = await store.SaveAsync(profile, tab, MakeBuffer("world"), 100);
                Assert.True(third);

                // Same content again is deduped once more.
                bool fourth = await store.SaveAsync(profile, tab, MakeBuffer("world"), 100);
                Assert.False(fourth);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public async Task MissingFileForcesAReWriteEvenWhenContentIsUnchanged()
        {
            string dir = Path.Combine(Path.GetTempPath(), "termrig-restore-" + Guid.NewGuid().ToString("N"));
            var store = new TerminalRestoreStore(dir);
            (TerminalProfile profile, TerminalTabProfile tab) = MakeIdentity();

            try
            {
                Assert.True(await store.SaveAsync(profile, tab, MakeBuffer("keep"), 100));

                // Delete the snapshot behind the store's back; identical content must still be written
                // so a deleted snapshot is never silently lost.
                string profileDir = Path.Combine(dir, "profile-fixed-id");
                foreach (string file in Directory.GetFiles(profileDir))
                {
                    File.Delete(file);
                }

                bool rewritten = await store.SaveAsync(profile, tab, MakeBuffer("keep"), 100);
                Assert.True(rewritten);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Fact]
        public async Task DeleteResetsDedupSoIdenticalContentIsWrittenAgain()
        {
            string dir = Path.Combine(Path.GetTempPath(), "termrig-restore-" + Guid.NewGuid().ToString("N"));
            var store = new TerminalRestoreStore(dir);
            (TerminalProfile profile, TerminalTabProfile tab) = MakeIdentity();

            try
            {
                Assert.True(await store.SaveAsync(profile, tab, MakeBuffer("data"), 100));
                await store.DeleteAsync(profile, tab, CancellationToken.None);

                // After an explicit delete the dedup signature is cleared, so an identical snapshot
                // writes a fresh file rather than being skipped.
                bool afterDelete = await store.SaveAsync(profile, tab, MakeBuffer("data"), 100);
                Assert.True(afterDelete);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
