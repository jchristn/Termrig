namespace Test.Terminal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Termrig.Core.Models;
    using Termrig.Core.Services;
    using Xunit;

    public class TabLayoutTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TabHeadersScrollOrWrapAtWorkspaceWidth(bool wrap)
        {
            var headers = new WrapPanel { ItemSpacing = 2, LineSpacing = 2 };
            for (int i = 0; i < 17; i++)
                headers.Children.Add(new Border { Width = 150, Height = 22 });

            // Horizontal scrolling measures content with unlimited width; disabling it
            // constrains the wrap panel to the viewport width.
            headers.Measure(new Size(wrap ? 900 : Double.PositiveInfinity, 600));
            headers.Arrange(new Rect(headers.DesiredSize));

            if (wrap)
            {
                Assert.True(headers.DesiredSize.Height > 22);
                Assert.True(headers.DesiredSize.Width <= 900);
            }
            else
            {
                Assert.Equal(22, headers.DesiredSize.Height);
                Assert.True(headers.DesiredSize.Width > 900);
            }
        }

        [Fact]
        public async Task ProfileLayoutDefaultsToScrollAndPersistsMultipleRows()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(directory);
                var store = new ProfileStore(directory);
                await File.WriteAllTextAsync(store.FilePath, "[{\"Name\":\"Legacy\"}]");
                Assert.False(Assert.Single(await store.LoadAsync()).WrapTabs);

                await store.SaveAsync(new List<TerminalProfile>
                {
                    new TerminalProfile { Name = "Wrapped", WrapTabs = true },
                    new TerminalProfile { Name = "Scrolling" }
                });
                var profiles = await store.LoadAsync();
                Assert.True(profiles[0].WrapTabs);
                Assert.False(profiles[1].WrapTabs);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
