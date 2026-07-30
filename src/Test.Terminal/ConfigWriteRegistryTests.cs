namespace Test.Terminal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Termrig.Core.Models;
    using Termrig.Core.Services;
    using Xunit;

    /// <summary>
    /// Covers <see cref="ConfigWriteRegistry"/> and its wiring into <see cref="ProfileStore"/>, which
    /// together stop the config write -> file-watcher -> full-reload feedback loop by recognizing a
    /// watcher event as an echo of the app's own write.
    /// </summary>
    public class ConfigWriteRegistryTests
    {
        private static string UniquePath()
        {
            return Path.Combine(Path.GetTempPath(), "termrig-cwr-" + Guid.NewGuid().ToString("N") + ".json");
        }

        [Fact]
        public void RecordedContentIsRecognizedAsAnEcho()
        {
            string path = UniquePath();
            byte[] content = Encoding.UTF8.GetBytes("[{\"a\":1}]");

            ConfigWriteRegistry.RecordWrite(path, content);

            // Negative (suppress): identical content is our own write and must be ignored by the watcher.
            Assert.True(ConfigWriteRegistry.IsEchoOfLastWrite(path, content));
        }

        [Fact]
        public void DifferentContentIsNotAnEcho()
        {
            string path = UniquePath();
            ConfigWriteRegistry.RecordWrite(path, Encoding.UTF8.GetBytes("[{\"a\":1}]"));

            // Positive (reload): an external edit changes the bytes and must not be suppressed.
            Assert.False(ConfigWriteRegistry.IsEchoOfLastWrite(path, Encoding.UTF8.GetBytes("[{\"a\":2}]")));
        }

        [Fact]
        public void UnknownPathIsNeverAnEcho()
        {
            Assert.False(ConfigWriteRegistry.IsEchoOfLastWrite(UniquePath(), Encoding.UTF8.GetBytes("x")));
        }

        [Fact]
        public async Task FileOverloadTracksOnDiskContent()
        {
            string path = UniquePath();
            byte[] content = Encoding.UTF8.GetBytes("[{\"hello\":\"world\"}]");

            try
            {
                ConfigWriteRegistry.RecordWrite(path, content);
                await File.WriteAllBytesAsync(path, content);
                Assert.True(ConfigWriteRegistry.IsEchoOfLastWrite(path));

                // Simulate an external editor changing the file.
                await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes("[]"));
                Assert.False(ConfigWriteRegistry.IsEchoOfLastWrite(path));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public async Task ProfileStoreSaveRegistersItsOwnWriteAsAnEcho()
        {
            string dir = Path.Combine(Path.GetTempPath(), "termrig-store-" + Guid.NewGuid().ToString("N"));
            var store = new ProfileStore(dir);

            try
            {
                await store.SaveAsync(new List<TerminalProfile> { new TerminalProfile { Name = "Workspace" } });

                // The store's own write must be recognized as an echo (no reload triggered).
                Assert.True(ConfigWriteRegistry.IsEchoOfLastWrite(store.FilePath));

                // An external modification must not be treated as an echo (reload should proceed).
                await File.WriteAllTextAsync(store.FilePath, "[]");
                Assert.False(ConfigWriteRegistry.IsEchoOfLastWrite(store.FilePath));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
