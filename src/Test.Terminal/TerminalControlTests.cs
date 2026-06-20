namespace Test.Terminal
{
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
                PtyRecordingDirectory = @"C:\Recordings"
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

            Assert.Equal("pwsh.exe", view.Process);
            Assert.Equal(new[] { "-NoLogo" }, view.Args);
            Assert.Equal(@"C:\Source", view.StartingDirectory);
            Assert.True(view.RecordPtyOutput);
            Assert.Equal(@"C:\Source\recordings", view.PtyRecordingDirectory);
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

        private static void SetPrivateField<T>(object instance, string name, T value)
        {
            instance
                .GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(instance, value);
        }
    }
}
