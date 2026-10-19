namespace Test.Terminal
{
    using System;
    using System.Reflection;
    using Avalonia.Input;
    using Termrig.App.Views;
    using Xunit;

    public class TerminalShortcutTests
    {
        private static object? Resolve(string method, KeyEventArgs args, bool isMacOS)
        {
            MethodInfo resolver = typeof(TerminalWorkspaceWindow).GetMethod(method,
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(method + " not found.");
            return resolver.Invoke(null, new object[] { args, isMacOS });
        }

        [Theory]
        [InlineData(Key.C, KeyModifiers.Meta, true, Key.C)]
        [InlineData(Key.V, KeyModifiers.Meta, true, Key.V)]
        // Both Command keys must be consumed before the terminal clears the selection.
        [InlineData(Key.LWin, KeyModifiers.Meta, true, Key.LWin)]
        [InlineData(Key.RWin, KeyModifiers.Meta, true, Key.RWin)]
        [InlineData(Key.LWin, KeyModifiers.Meta | KeyModifiers.Shift, true, Key.LWin)]
        [InlineData(Key.C, KeyModifiers.Control, true, Key.None)]
        [InlineData(Key.C, KeyModifiers.None, true, Key.None)]
        [InlineData(Key.V, KeyModifiers.Meta | KeyModifiers.Alt, true, Key.None)]
        [InlineData(Key.C, KeyModifiers.Meta | KeyModifiers.Shift, true, Key.None)]
        [InlineData(Key.C, KeyModifiers.Meta | KeyModifiers.Control, true, Key.None)]
        [InlineData(Key.C, KeyModifiers.Meta, false, Key.None)]
        [InlineData(Key.V, KeyModifiers.Meta, false, Key.None)]
        [InlineData(Key.LWin, KeyModifiers.Meta, false, Key.None)]
        [InlineData(Key.W, KeyModifiers.Meta, true, Key.None)]
        public void MacClipboardAndModifierKeysAreRoutedWithoutConsumingOtherShortcuts(
            Key key, KeyModifiers modifiers, bool isMacOS, Key expected)
        {
            Assert.Equal(expected, (Key)Resolve("GetMacTerminalShortcut",
                new KeyEventArgs { Key = key, KeyModifiers = modifiers }, isMacOS)!);
        }

        [Theory]
        [InlineData(Key.OemPlus, KeyModifiers.Meta, true, 1)]
        [InlineData(Key.OemPlus, KeyModifiers.Meta | KeyModifiers.Shift, true, 1)]
        [InlineData(Key.OemMinus, KeyModifiers.Meta, true, -1)]
        [InlineData(Key.D0, KeyModifiers.Meta, true, 0)]
        [InlineData(Key.NumPad0, KeyModifiers.Meta, true, 0)]
        [InlineData(Key.Add, KeyModifiers.Meta, true, 1)]
        [InlineData(Key.Subtract, KeyModifiers.Meta, true, -1)]
        [InlineData(Key.OemPlus, KeyModifiers.Control, false, 1)]
        [InlineData(Key.OemMinus, KeyModifiers.Control, false, -1)]
        [InlineData(Key.D0, KeyModifiers.Control, false, 0)]
        [InlineData(Key.OemPlus, KeyModifiers.Control, true, null)]
        [InlineData(Key.OemPlus, KeyModifiers.Meta, false, null)]
        [InlineData(Key.OemPlus, KeyModifiers.None, true, null)]
        [InlineData(Key.OemPlus, KeyModifiers.Meta | KeyModifiers.Alt, true, null)]
        [InlineData(Key.OemPlus, KeyModifiers.Control | KeyModifiers.Alt, false, null)]
        [InlineData(Key.C, KeyModifiers.Meta, true, null)]
        public void FontShortcutsUsePlatformModifierAndExcludeAlt(
            Key key, KeyModifiers modifiers, bool isMacOS, int? expected)
        {
            Assert.Equal(expected, (int?)Resolve("GetTerminalFontSizeChange",
                new KeyEventArgs { Key = key, KeyModifiers = modifiers }, isMacOS));
        }

        [Theory]
        [InlineData(PhysicalKey.Equal, 1)]
        [InlineData(PhysicalKey.Minus, -1)]
        [InlineData(PhysicalKey.Digit0, 0)]
        public void MacFontShortcutsSupportPhysicalKeysOnOtherLayouts(PhysicalKey key, int expected)
        {
            Assert.Equal(expected, (int?)Resolve("GetTerminalFontSizeChange",
                new KeyEventArgs { PhysicalKey = key, KeyModifiers = KeyModifiers.Meta }, true));
        }
    }
}
