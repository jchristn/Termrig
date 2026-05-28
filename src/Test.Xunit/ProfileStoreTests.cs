namespace Test.Xunit
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Termrig.Core.Enums;
    using Termrig.Core.Models;
    using Termrig.Core.Services;
    using Xunit;

    /// <summary>
    /// Tests for profile persistence.
    /// </summary>
    public class ProfileStoreTests
    {
        /// <summary>
        /// Profiles are persisted and loaded from the configured directory.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task SaveAndLoadProfiles()
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                ProfileStore store = new ProfileStore(directory);
                List<TerminalProfile> profiles = new List<TerminalProfile>
                {
                    new TerminalProfile
                    {
                        Name = "Work",
                        Tabs = new List<TerminalTabProfile>
                        {
                            new TerminalTabProfile
                            {
                                Name = "PowerShell",
                                Shell = ShellType.PowerShell,
                                StartingDirectory = "C:\\Code",
                                StartupScript = "Write-Host ready",
                                FontFamily = "Consolas",
                                FontSize = 16
                            }
                        }
                    }
                };

                await store.SaveAsync(profiles, CancellationToken.None).ConfigureAwait(true);
                List<TerminalProfile> loaded = await store.LoadAsync(CancellationToken.None).ConfigureAwait(true);

                Assert.Single(loaded);
                Assert.Equal("Work", loaded[0].Name);
                Assert.Single(loaded[0].Tabs);
                Assert.Equal(ShellType.PowerShell, loaded[0].Tabs[0].Shell);
                Assert.Equal("Write-Host ready", loaded[0].Tabs[0].StartupScript);
                Assert.Equal("Consolas", loaded[0].Tabs[0].FontFamily);
                Assert.Equal(16, loaded[0].Tabs[0].FontSize);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Upsert replaces an existing profile with the same identifier.
        /// </summary>
        /// <returns>Task.</returns>
        [Fact]
        public async Task UpsertReplacesExistingProfile()
        {
            string directory = Path.Combine(Path.GetTempPath(), "termrig-tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                ProfileStore store = new ProfileStore(directory);
                TerminalProfile profile = new TerminalProfile { Id = "profile1", Name = "Old" };
                await store.UpsertAsync(profile, CancellationToken.None).ConfigureAwait(true);

                profile.Name = "New";
                await store.UpsertAsync(profile, CancellationToken.None).ConfigureAwait(true);

                List<TerminalProfile> loaded = await store.LoadAsync(CancellationToken.None).ConfigureAwait(true);
                Assert.Single(loaded);
                Assert.Equal("New", loaded[0].Name);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
