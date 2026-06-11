namespace Test.Terminal
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Xunit;
    using Xunit.Abstractions;

    public class PtyRecordingAnalysisTests
    {
        private readonly ITestOutputHelper _output;

        public PtyRecordingAnalysisTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "ManualRecording")]
        public void RecordingFromEnvironmentReplaysDeterministically()
        {
            string? recording = Environment.GetEnvironmentVariable("TERMRIG_PTY_RECORDING");
            if (String.IsNullOrWhiteSpace(recording))
            {
                _output.WriteLine("Set TERMRIG_PTY_RECORDING to a .pty.bin or .pty.json file to analyze a captured recording.");
                return;
            }

            AnalyzeOne(recording);
        }

        [Fact]
        public void CheckedInPtyRecordingFixturesReplayDeterministically()
        {
            string fixtureDirectory = Environment.GetEnvironmentVariable("TERMRIG_PTY_FIXTURE_DIR")
                ?? Path.Combine(AppContext.BaseDirectory, "Fixtures", "PtyRecordings");

            if (!Directory.Exists(fixtureDirectory))
            {
                _output.WriteLine("No PTY recording fixture directory exists: " + fixtureDirectory);
                return;
            }

            string[] recordings = Directory.GetFiles(fixtureDirectory, "*.pty.bin", SearchOption.AllDirectories);
            if (recordings.Length == 0)
            {
                _output.WriteLine("No .pty.bin fixtures were found in: " + fixtureDirectory);
                return;
            }

            var failures = new List<string>();
            foreach (string recording in recordings)
            {
                try
                {
                    AnalyzeOne(recording);
                }
                catch (Exception ex)
                {
                    failures.Add(recording + Environment.NewLine + ex);
                }
            }

            Assert.Empty(failures);
        }

        private void AnalyzeOne(string recording)
        {
            PtyRecordingFixture fixture = PtyRecordingFixture.Load(recording);
            PtyRecordingAnalysisResult result = PtyRecordingAnalyzer.Analyze(fixture);
            _output.WriteLine(result.Report);

            string? reportPath = Environment.GetEnvironmentVariable("TERMRIG_PTY_ANALYSIS_WRITE_REPORT");
            if (!String.IsNullOrWhiteSpace(reportPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)) ?? Directory.GetCurrentDirectory());
                File.WriteAllText(reportPath, result.Report);
            }

            string? snapshotPath = Environment.GetEnvironmentVariable("TERMRIG_PTY_ANALYSIS_WRITE_SNAPSHOT");
            if (!String.IsNullOrWhiteSpace(snapshotPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(snapshotPath)) ?? Directory.GetCurrentDirectory());
                File.WriteAllText(snapshotPath, PtyRecordingAnalyzer.SnapshotToJson(result.BaselineSnapshot));
            }

            Assert.Empty(result.DeterminismMismatches);

            if (File.Exists(fixture.ExpectedSnapshotPath))
            {
                string expectedJson = PtyRecordingAnalyzer.NormalizeJson(File.ReadAllText(fixture.ExpectedSnapshotPath));
                string actualJson = PtyRecordingAnalyzer.NormalizeJson(PtyRecordingAnalyzer.SnapshotToJson(result.BaselineSnapshot));
                Assert.Equal(expectedJson, actualJson);
            }

            if (String.Equals(Environment.GetEnvironmentVariable("TERMRIG_PTY_ANALYSIS_FAIL_ON_WARNINGS"), "1", StringComparison.Ordinal))
                Assert.Empty(result.Warnings);
        }
    }
}
