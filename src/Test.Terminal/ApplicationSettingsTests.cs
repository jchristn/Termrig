namespace Test.Terminal
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Termrig.Core.Models;
    using Termrig.Core.Services;
    using Xunit;

    public sealed class ApplicationSettingsTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "termrig-settings-" + Guid.NewGuid());

        [Fact]
        public async Task MissingSettingsEnableUnfocusedUpdatesWithoutCreatingFiles()
        {
            Assert.True((await new ApplicationSettingsStore(_directory).LoadAsync()).UpdateUnfocusedWindows);
            Assert.False(Directory.Exists(_directory));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task PreferenceSurvivesRestartAndCanBeChanged(bool enabled)
        {
            var store = new ApplicationSettingsStore(_directory);
            await store.SaveAsync(new ApplicationSettings { UpdateUnfocusedWindows = enabled });
            Assert.Equal(enabled, (await new ApplicationSettingsStore(_directory).LoadAsync()).UpdateUnfocusedWindows);
            await store.SaveAsync(new ApplicationSettings { UpdateUnfocusedWindows = !enabled });
            Assert.Equal(!enabled, (await new ApplicationSettingsStore(_directory).LoadAsync()).UpdateUnfocusedWindows);
            Assert.False(File.Exists(Path.Combine(_directory, "settings.json.tmp")));
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("null")]
        public async Task SettingsWithoutPreferenceUseEnabledDefault(string json)
        {
            Directory.CreateDirectory(_directory);
            await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), json);
            Assert.True((await new ApplicationSettingsStore(_directory).LoadAsync()).UpdateUnfocusedWindows);
        }

        [Fact]
        public async Task FailedSavePreservesPreviouslySavedPreference()
        {
            var store = new ApplicationSettingsStore(_directory);
            await store.SaveAsync(new ApplicationSettings { UpdateUnfocusedWindows = false });
            // A directory at the staging path prevents writing the replacement file.
            Directory.CreateDirectory(Path.Combine(_directory, "settings.json.tmp"));
            Exception? error = await Record.ExceptionAsync(() => store.SaveAsync(
                new ApplicationSettings { UpdateUnfocusedWindows = true }));
            Assert.True(error is IOException or UnauthorizedAccessException);
            Assert.False((await store.LoadAsync()).UpdateUnfocusedWindows);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
    }
}
