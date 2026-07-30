namespace Test.Terminal
{
    using System;
    using System.Reflection;
    using Termrig.App.Views;
    using Xunit;

    /// <summary>
    /// Covers the render-activation predicate that decides whether a workspace terminal should defer
    /// painting. A background or minimized window must pause even its selected tab so it stops
    /// competing for the shared UI thread; parsing continues regardless, so no output is lost.
    /// </summary>
    public class RenderActivationTests
    {
        private static bool ShouldPauseRendering(bool windowRenderActive, bool isSelectedTab)
        {
            MethodInfo method = typeof(TerminalWorkspaceWindow).GetMethod(
                "ShouldPauseRendering",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("ShouldPauseRendering not found.");

            return (bool)method.Invoke(null, new object[] { windowRenderActive, isSelectedTab })!;
        }

        [Fact]
        public void ActiveWindowSelectedTabRenders()
        {
            // Positive: the one terminal the user is looking at renders.
            Assert.False(ShouldPauseRendering(windowRenderActive: true, isSelectedTab: true));
        }

        [Fact]
        public void ActiveWindowUnselectedTabPauses()
        {
            Assert.True(ShouldPauseRendering(windowRenderActive: true, isSelectedTab: false));
        }

        [Fact]
        public void InactiveWindowSelectedTabPauses()
        {
            // The regression this fixes: a background/minimized window's selected tab used to keep
            // rendering. It must now pause.
            Assert.True(ShouldPauseRendering(windowRenderActive: false, isSelectedTab: true));
        }

        [Fact]
        public void InactiveWindowUnselectedTabPauses()
        {
            Assert.True(ShouldPauseRendering(windowRenderActive: false, isSelectedTab: false));
        }
    }
}
