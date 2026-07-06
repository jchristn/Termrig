namespace Test.Terminal
{
    using Avalonia.Input;
    using Iciclecreek.Terminal;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Xunit;

    public class TerminalControlTests
    {
        [Fact]
        public void LaunchPropertySyncCopiesPtyRecordingSettingsToTerminalView()
        {
            var args = new List<string> { "/D", "/K" };
            var options = new XTerm.Options.TerminalOptions
            {
                ConvertEol = false,
                Scrollback = 1234,
                TermName = "xterm-test"
            };

            var control = new TerminalControl
            {
                Process = "cmd-test.exe",
                Args = args,
                StartingDirectory = @"C:\Work",
                Options = options,
                RecordPtyOutput = true,
                PtyRecordingDirectory = @"C:\Recordings",
                IsRenderPaused = true
            };
            var view = new TerminalView();

            typeof(TerminalControl)
                .GetField("_terminalView", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(control, view);

            typeof(TerminalControl)
                .GetMethod("ApplyPropertiesToTerminalView", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(control, null);

            Assert.Equal("cmd-test.exe", view.Process);
            Assert.Same(args, view.Args);
            Assert.Equal(@"C:\Work", view.StartingDirectory);
            Assert.Same(options, view.Options);
            Assert.True(view.RecordPtyOutput);
            Assert.Equal(@"C:\Recordings", view.PtyRecordingDirectory);
            Assert.True(view.IsRenderPaused);
        }

        [Fact]
        public void LaunchPropertyChangesAreAppliedToExistingTerminalView()
        {
            var control = new TerminalControl();
            var view = new TerminalView();

            typeof(TerminalControl)
                .GetField("_terminalView", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(control, view);

            control.Process = "pwsh.exe";
            control.Args = new[] { "-NoLogo" };
            control.StartingDirectory = @"C:\Source";
            control.RecordPtyOutput = true;
            control.PtyRecordingDirectory = @"C:\Source\recordings";
            control.IsRenderPaused = true;

            Assert.Equal("pwsh.exe", view.Process);
            Assert.Equal(new[] { "-NoLogo" }, view.Args);
            Assert.Equal(@"C:\Source", view.StartingDirectory);
            Assert.True(view.RecordPtyOutput);
            Assert.Equal(@"C:\Source\recordings", view.PtyRecordingDirectory);
            Assert.True(view.IsRenderPaused);
        }

        [Fact]
        public void RenderPauseDefersInvalidationUntilResumed()
        {
            var view = new TerminalView
            {
                IsRenderPaused = true
            };

            typeof(TerminalView)
                .GetMethod("RequestInvalidate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(view, null);

            Assert.True(GetPrivateField<bool>(view, "_renderInvalidatePending"));

            view.IsRenderPaused = false;

            Assert.False(GetPrivateField<bool>(view, "_renderInvalidatePending"));
        }

        [Fact]
        public void OutputReceivedDispatchIsCoalescedWhilePending()
        {
            var view = new TerminalView();
            MethodInfo dispatch = typeof(TerminalView)
                .GetMethod("DispatchOutputReceived", BindingFlags.Instance | BindingFlags.NonPublic)!;

            dispatch.Invoke(view, null);
            dispatch.Invoke(view, null);

            Assert.Equal(1, GetPrivateField<int>(view, "_outputReceivedScheduled"));
        }

        [Fact]
        public async Task OpenPtyRecordingUsesLaunchSnapshotOffUiThread()
        {
            string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(directory);

            try
            {
                var view = new TerminalView();
                SetPrivateField(view, "_recordCurrentPtyOutput", true);
                SetPrivateField(view, "_currentPtyRecordingDirectory", directory);
                SetPrivateField(view, "_currentPtyRecordingProcessName", "cmd.exe");
                SetPrivateField(view, "_currentPtyRecordingArguments", new[] { "/D", "/K" });
                SetPrivateField(view, "_currentPtyRecordingStartingDirectory", @"C:\Users\joelc");

                object? recording = await Task.Run(() =>
                    typeof(TerminalView)
                        .GetMethod("OpenPtyRecording", BindingFlags.Instance | BindingFlags.NonPublic)!
                        .Invoke(view, null));

                Assert.NotNull(recording);
                await ((IAsyncDisposable)recording!).DisposeAsync();

                Assert.Single(Directory.GetFiles(directory, "*.pty.bin"));
                string metadataPath = Assert.Single(Directory.GetFiles(directory, "*.pty.json"));
                string metadata = await File.ReadAllTextAsync(metadataPath);
                Assert.Contains("\"Process\": \"cmd.exe\"", metadata);
                Assert.Contains("\"/D\"", metadata);
                Assert.Contains("\"StartingDirectory\": \"C:\\\\Users\\\\joelc\"", metadata);
                Assert.Contains("\"UsesLineEndingPaddingNormalizer\": false", metadata);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Theory]
        [InlineData(Key.LeftShift, KeyModifiers.Shift)]
        [InlineData(Key.RightShift, KeyModifiers.Shift)]
        [InlineData(Key.LeftCtrl, KeyModifiers.Control)]
        [InlineData(Key.RightCtrl, KeyModifiers.Control)]
        [InlineData(Key.LeftAlt, KeyModifiers.Alt)]
        [InlineData(Key.RightAlt, KeyModifiers.Alt)]
        [InlineData(Key.LWin, KeyModifiers.Meta)]
        [InlineData(Key.RWin, KeyModifiers.Meta)]
        public void Win32InputSequenceSuppressesModifierOnlyKeys(Key key, KeyModifiers modifiers)
        {
            var view = new TerminalView();
            var args = new KeyEventArgs
            {
                Key = key,
                KeyModifiers = modifiers
            };

            string keyDown = GenerateWin32InputSequence(view, args, true);
            string keyUp = GenerateWin32InputSequence(view, args, false);

            Assert.Equal(String.Empty, keyDown);
            Assert.Equal(String.Empty, keyUp);
        }

        [Fact]
        public void Win32InputSequencePreservesShiftedPrintableCharacters()
        {
            var view = new TerminalView();
            var args = new KeyEventArgs
            {
                Key = Key.D4,
                KeyModifiers = KeyModifiers.Shift,
                KeySymbol = "$"
            };

            string keyDown = GenerateWin32InputSequence(view, args, true);
            string keyUp = GenerateWin32InputSequence(view, args, false);

            Assert.Equal("\u001b[52;0;36;1;16;1_", keyDown);
            Assert.Equal("\u001b[52;0;36;0;16;1_", keyUp);
        }

        private static string GenerateWin32InputSequence(TerminalView view, KeyEventArgs args, bool isKeyDown)
        {
            return (string)typeof(TerminalView)
                .GetMethod("GenerateWin32InputSequence", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(view, new object[] { args, isKeyDown })!;
        }

        private static void SetPrivateField<T>(object instance, string name, T value)
        {
            instance
                .GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(instance, value);
        }

        private static T GetPrivateField<T>(object instance, string name)
        {
            return (T)instance
                .GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(instance)!;
        }
    }
}
