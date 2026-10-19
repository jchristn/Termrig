namespace Test.Terminal
{
    using System;
    using System.Reflection;
    using Termrig.App.Views;
    using Xunit;

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

        [Theory]
        [InlineData(true, false, true, false)]
        [InlineData(true, false, false, false)]
        [InlineData(false, false, true, false)]
        [InlineData(false, false, false, true)]
        [InlineData(true, true, true, true)]
        [InlineData(true, true, false, true)]
        [InlineData(false, true, true, true)]
        [InlineData(false, true, false, true)]
        public void WorkspacePolicyHonorsFocusMinimizationAndPreference(
            bool active, bool minimized, bool updateUnfocused, bool expectedPaused)
        {
            MethodInfo method = typeof(TerminalWorkspaceWindow).GetMethod(
                "CanRenderWorkspace", BindingFlags.Static | BindingFlags.NonPublic)!;
            bool canRender = (bool)method.Invoke(null, new object[] { active, minimized, updateUnfocused })!;
            Assert.Equal(expectedPaused, ShouldPauseRendering(canRender, isSelectedTab: true));
            Assert.True(ShouldPauseRendering(canRender, isSelectedTab: false));
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
            Assert.True(ShouldPauseRendering(windowRenderActive: false, isSelectedTab: true));
        }

        [Fact]
        public void InactiveWindowUnselectedTabPauses()
        {
            Assert.True(ShouldPauseRendering(windowRenderActive: false, isSelectedTab: false));
        }
    }
}
