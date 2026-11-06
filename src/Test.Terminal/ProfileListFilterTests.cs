namespace Test.Terminal
{
    using System.Linq;
    using Termrig.App.Services;
    using Termrig.Core.Models;
    using Xunit;

    public class ProfileListFilterTests
    {
        [Theory]
        [InlineData("prod")]
        [InlineData("PROD")]
        [InlineData("  Prod  ")]
        public void MatchesProfileNamesByCaseInsensitiveSubstring(string query)
        {
            var profiles = new[]
            {
                new TerminalProfile { Name = "Development" },
                new TerminalProfile { Name = "API Production" },
                new TerminalProfile { Name = "Production worker" }
            };

            var items = ProfileListFilter.BuildItems(profiles, new ProfileFolder[0], query);

            Assert.Equal(new[] { profiles[1], profiles[2] }, items.Select(item => item.Profile));
            Assert.Equal("Development", profiles[0].Name);
        }

        [Fact]
        public void RevealsMatchingChildrenWithoutChangingFolderExpansionOrOrder()
        {
            var folder = new ProfileFolder { Name = "Servers", IsExpanded = false };
            var emptyFolder = new ProfileFolder { Name = "Production archives" };
            var first = new TerminalProfile { Name = "Production A", FolderId = folder.Id };
            var hidden = new TerminalProfile { Name = "Development", FolderId = folder.Id };
            var second = new TerminalProfile { Name = "Production B", FolderId = folder.Id };
            var root = new TerminalProfile { Name = "Production root" };
            var profiles = new[] { first, hidden, root, second };
            var folders = new[] { folder, emptyFolder };

            var items = ProfileListFilter.BuildItems(profiles, folders, "prod");

            Assert.Equal(new[] { root.Name, folder.Name, first.Name, second.Name }, items.Select(item => item.DisplayName));
            Assert.True(items[1].IsFilterExpanded);
            Assert.False(items[1].CanToggleExpansion);
            Assert.False(folder.IsExpanded);
            Assert.Equal(new[] { first, hidden, root, second }, profiles);

            var cleared = ProfileListFilter.BuildItems(profiles, folders, "");
            Assert.Equal(new[] { root.Name, folder.Name, emptyFolder.Name }, cleared.Select(item => item.DisplayName));
            Assert.False(cleared[1].IsFilterExpanded);
            Assert.True(cleared[1].CanToggleExpansion);
            Assert.False(folder.IsExpanded);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyFilterPreservesTheNormalFolderLayout(string? query)
        {
            var open = new ProfileFolder { Name = "Open" };
            var closed = new ProfileFolder { Name = "Closed", IsExpanded = false };
            var root = new TerminalProfile { Name = "Root" };
            var child = new TerminalProfile { Name = "Child", FolderId = open.Id };
            var hidden = new TerminalProfile { Name = "Hidden", FolderId = closed.Id };

            var items = ProfileListFilter.BuildItems(new[] { child, hidden, root }, new[] { open, closed }, query);

            Assert.Equal(new[] { "Root", "Open", "Child", "Closed" }, items.Select(item => item.DisplayName));
            Assert.All(items, item => Assert.False(item.IsFilterExpanded));
        }

        [Fact]
        public void UnmatchedQueryReturnsNoRowsEvenWhenFolderNameMatches()
        {
            var folder = new ProfileFolder { Name = "Production" };
            var profile = new TerminalProfile { Name = "Development", FolderId = folder.Id };

            Assert.Empty(ProfileListFilter.BuildItems(new[] { profile }, new[] { folder }, "production"));
        }

        [Fact]
        public void FilteringRetainsOnlyVisibleSelectionsAndNeverSelectsAnotherProfile()
        {
            var folder = new ProfileFolder { Name = "Servers", IsExpanded = false };
            var match = new TerminalProfile { Name = "Production", FolderId = folder.Id };
            var hidden = new TerminalProfile { Name = "Development" };
            var items = ProfileListFilter.BuildItems(new[] { hidden, match }, new[] { folder }, "prod");

            Assert.Same(match, ProfileListFilter.FindSelection(items, match.Id, null)?.Profile);
            Assert.Same(folder, ProfileListFilter.FindSelection(items, null, folder.Id)?.Folder);
            Assert.Null(ProfileListFilter.FindSelection(items, hidden.Id, null));
            Assert.Null(ProfileListFilter.FindSelection(items, null, null));

            var cleared = ProfileListFilter.BuildItems(new[] { hidden, match }, new[] { folder }, "");
            Assert.Null(ProfileListFilter.FindSelection(cleared, match.Id, null));
            Assert.False(folder.IsExpanded);
        }

        [Fact]
        public void RefreshUsesCurrentProfileObjectsAndAppliesRenames()
        {
            var original = new TerminalProfile { Name = "Production" };
            var reloaded = new TerminalProfile { Id = original.Id, Name = original.Name };
            var items = ProfileListFilter.BuildItems(new[] { reloaded }, new ProfileFolder[0], "prod");
            Assert.Same(reloaded, ProfileListFilter.FindSelection(items, original.Id, null)?.Profile);

            reloaded.Name = "Development";
            items = ProfileListFilter.BuildItems(new[] { reloaded }, new ProfileFolder[0], "prod");
            Assert.Empty(items);
            Assert.Null(ProfileListFilter.FindSelection(items, original.Id, null));
        }
    }
}
