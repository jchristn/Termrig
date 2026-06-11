namespace Test.Terminal
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;

    internal sealed class PtyRecordingMetadata
    {
        public int FormatVersion { get; set; } = 1;
        public string RecordingFile { get; set; } = String.Empty;
        public string StartedAtUtc { get; set; } = String.Empty;
        public string? EndedAtUtc { get; set; }
        public string Process { get; set; } = String.Empty;
        public string[] Arguments { get; set; } = Array.Empty<string>();
        public string StartingDirectory { get; set; } = String.Empty;
        public int Columns { get; set; }
        public int Rows { get; set; }
        public int Scrollback { get; set; } = 1000;
        public bool ConvertEol { get; set; }
        public string? TermName { get; set; } = "xterm-256color";
        public bool UsesLineEndingPaddingNormalizer { get; set; }
        public bool PinsViewportToBottomAfterWrite { get; set; }
        public string OSDescription { get; set; } = String.Empty;
        public string OSArchitecture { get; set; } = String.Empty;
        public string FrameworkDescription { get; set; } = String.Empty;
        public string? TermEnvironment { get; set; }
        public string? ColorTermEnvironment { get; set; }
        public long ByteCount { get; set; }
        public List<int> ChunkSizes { get; set; } = new List<int>();
    }

    internal sealed class PtyRecordingFixture
    {
        private PtyRecordingFixture(
            string recordingPath,
            string? metadataPath,
            PtyRecordingMetadata metadata,
            byte[] bytes,
            IReadOnlyList<byte[]> capturedChunks)
        {
            RecordingPath = recordingPath;
            MetadataPath = metadataPath;
            Metadata = metadata;
            Bytes = bytes;
            CapturedChunks = capturedChunks;
        }

        public string RecordingPath { get; }

        public string? MetadataPath { get; }

        public PtyRecordingMetadata Metadata { get; }

        public byte[] Bytes { get; }

        public IReadOnlyList<byte[]> CapturedChunks { get; }

        public string ExpectedSnapshotPath => GetCompanionPath(RecordingPath, ".pty.expected.json");

        public static PtyRecordingFixture Load(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentNullException(nameof(path));

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("PTY recording file was not found.", fullPath);

            string recordingPath = fullPath.EndsWith(".pty.json", StringComparison.OrdinalIgnoreCase)
                ? ResolveRecordingPathFromMetadata(fullPath)
                : fullPath;

            string? metadataPath = ResolveMetadataPath(recordingPath, fullPath);
            PtyRecordingMetadata metadata = metadataPath != null
                ? LoadMetadata(metadataPath)
                : CreateMetadataFromEnvironment(recordingPath);

            byte[] bytes = File.ReadAllBytes(recordingPath);
            IReadOnlyList<byte[]> capturedChunks = SplitCapturedChunks(bytes, metadata.ChunkSizes);
            return new PtyRecordingFixture(recordingPath, metadataPath, metadata, bytes, capturedChunks);
        }

        public TerminalReplayOptions ToReplayOptions()
        {
            if (Metadata.Columns <= 0 || Metadata.Rows <= 0)
            {
                throw new InvalidOperationException(
                    "Recording metadata must include positive Columns and Rows. " +
                    "For legacy .pty.bin files, set TERMRIG_PTY_COLUMNS and TERMRIG_PTY_ROWS.");
            }

            return new TerminalReplayOptions(
                Columns: Metadata.Columns,
                Rows: Metadata.Rows,
                ConvertEol: Metadata.ConvertEol,
                TrimLineEndingPadding: Metadata.UsesLineEndingPaddingNormalizer,
                Scrollback: Metadata.Scrollback > 0 ? Metadata.Scrollback : 1000,
                TermName: String.IsNullOrWhiteSpace(Metadata.TermName) ? "xterm-256color" : Metadata.TermName!,
                PinViewportToBottomAfterWrite: Metadata.PinsViewportToBottomAfterWrite);
        }

        private static string ResolveRecordingPathFromMetadata(string metadataPath)
        {
            PtyRecordingMetadata metadata = LoadMetadata(metadataPath);
            string directory = Path.GetDirectoryName(metadataPath) ?? Directory.GetCurrentDirectory();
            string recordingFile = String.IsNullOrWhiteSpace(metadata.RecordingFile)
                ? Path.GetFileName(GetCompanionPath(metadataPath, ".pty.bin"))
                : metadata.RecordingFile;

            return Path.GetFullPath(Path.Combine(directory, recordingFile));
        }

        private static string? ResolveMetadataPath(string recordingPath, string requestedPath)
        {
            if (requestedPath.EndsWith(".pty.json", StringComparison.OrdinalIgnoreCase))
                return requestedPath;

            string companion = GetCompanionPath(recordingPath, ".pty.json");
            return File.Exists(companion) ? companion : null;
        }

        private static PtyRecordingMetadata LoadMetadata(string metadataPath)
        {
            string json = File.ReadAllText(metadataPath, Encoding.UTF8);
            var metadata = JsonSerializer.Deserialize<PtyRecordingMetadata>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return metadata ?? throw new InvalidOperationException("PTY recording metadata was empty: " + metadataPath);
        }

        private static PtyRecordingMetadata CreateMetadataFromEnvironment(string recordingPath)
        {
            int columns = GetRequiredEnvironmentInt("TERMRIG_PTY_COLUMNS", recordingPath);
            int rows = GetRequiredEnvironmentInt("TERMRIG_PTY_ROWS", recordingPath);

            return new PtyRecordingMetadata
            {
                RecordingFile = Path.GetFileName(recordingPath),
                Columns = columns,
                Rows = rows,
                Scrollback = GetEnvironmentInt("TERMRIG_PTY_SCROLLBACK") ?? 1000,
                ConvertEol = GetEnvironmentBool("TERMRIG_PTY_CONVERT_EOL") ?? false,
                TermName = Environment.GetEnvironmentVariable("TERMRIG_PTY_TERM_NAME") ?? "xterm-256color",
                UsesLineEndingPaddingNormalizer = GetEnvironmentBool("TERMRIG_PTY_TRIM_LINE_ENDING_PADDING") ?? false,
                PinsViewportToBottomAfterWrite = GetEnvironmentBool("TERMRIG_PTY_PIN_VIEWPORT_TO_BOTTOM") ?? true
            };
        }

        private static int GetRequiredEnvironmentInt(string name, string recordingPath)
        {
            int? value = GetEnvironmentInt(name);
            if (value.HasValue)
                return value.Value;

            throw new InvalidOperationException(
                "Recording has no sidecar metadata: " + recordingPath + Environment.NewLine +
                "Set " + name + " or capture again with a build that writes .pty.json sidecars.");
        }

        private static int? GetEnvironmentInt(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            return Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : null;
        }

        private static bool? GetEnvironmentBool(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (String.IsNullOrWhiteSpace(value))
                return null;

            if (Boolean.TryParse(value, out bool parsed))
                return parsed;

            if (value == "1") return true;
            if (value == "0") return false;
            return null;
        }

        private static IReadOnlyList<byte[]> SplitCapturedChunks(byte[] bytes, IReadOnlyList<int> chunkSizes)
        {
            if (chunkSizes.Count == 0 || chunkSizes.Any(size => size <= 0) || chunkSizes.Sum(size => (long)size) != bytes.LongLength)
                return new[] { bytes };

            var chunks = new List<byte[]>(chunkSizes.Count);
            int offset = 0;
            foreach (int size in chunkSizes)
            {
                var chunk = new byte[size];
                Buffer.BlockCopy(bytes, offset, chunk, 0, size);
                chunks.Add(chunk);
                offset += size;
            }

            return chunks;
        }

        private static string GetCompanionPath(string path, string suffix)
        {
            if (path.EndsWith(".pty.bin", StringComparison.OrdinalIgnoreCase))
                return path[..^".pty.bin".Length] + suffix;

            if (path.EndsWith(".pty.json", StringComparison.OrdinalIgnoreCase))
                return path[..^".pty.json".Length] + suffix;

            return Path.ChangeExtension(path, suffix.TrimStart('.'));
        }
    }

    internal sealed class PtyRecordingAnalysisResult
    {
        public PtyRecordingAnalysisResult(
            TerminalSnapshot baselineSnapshot,
            IReadOnlyList<string> determinismMismatches,
            IReadOnlyList<string> warnings,
            string report)
        {
            BaselineSnapshot = baselineSnapshot;
            DeterminismMismatches = determinismMismatches;
            Warnings = warnings;
            Report = report;
        }

        public TerminalSnapshot BaselineSnapshot { get; }

        public IReadOnlyList<string> DeterminismMismatches { get; }

        public IReadOnlyList<string> Warnings { get; }

        public string Report { get; }
    }

    internal static class PtyRecordingAnalyzer
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public static PtyRecordingAnalysisResult Analyze(PtyRecordingFixture fixture)
        {
            TerminalReplayOptions options = fixture.ToReplayOptions();
            TerminalSnapshot baseline = TerminalReplay.Replay(options, fixture.CapturedChunks);

            var mismatches = new List<string>();
            CompareSnapshots(mismatches, "captured chunks", baseline, "single chunk", TerminalReplay.Replay(options, new[] { fixture.Bytes }));
            CompareSnapshots(mismatches, "captured chunks", baseline, "single byte chunks", TerminalReplay.Replay(options, SplitEveryByte(fixture.Bytes)));
            CompareSnapshots(mismatches, "captured chunks", baseline, "seeded chunks", TerminalReplay.Replay(options, SplitSeeded(fixture.Bytes, seed: 8675309)));

            IReadOnlyList<string> warnings = FindWarnings(baseline);
            string report = BuildReport(fixture, baseline, mismatches, warnings);
            return new PtyRecordingAnalysisResult(baseline, mismatches, warnings, report);
        }

        public static string SnapshotToJson(TerminalSnapshot snapshot)
        {
            return JsonSerializer.Serialize(snapshot, JsonOptions);
        }

        public static string NormalizeJson(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, JsonOptions);
        }

        private static void CompareSnapshots(
            List<string> mismatches,
            string expectedName,
            TerminalSnapshot expected,
            string actualName,
            TerminalSnapshot actual)
        {
            if (expected.Columns != actual.Columns)
                mismatches.Add($"{actualName}: Columns {actual.Columns} did not match {expectedName} {expected.Columns}.");
            if (expected.Rows != actual.Rows)
                mismatches.Add($"{actualName}: Rows {actual.Rows} did not match {expectedName} {expected.Rows}.");
            if (expected.CursorColumn != actual.CursorColumn || expected.CursorRow != actual.CursorRow)
                mismatches.Add($"{actualName}: cursor ({actual.CursorColumn},{actual.CursorRow}) did not match {expectedName} ({expected.CursorColumn},{expected.CursorRow}).");
            if (expected.ViewportY != actual.ViewportY)
                mismatches.Add($"{actualName}: ViewportY {actual.ViewportY} did not match {expectedName} {expected.ViewportY}.");
            if (expected.YBase != actual.YBase)
                mismatches.Add($"{actualName}: YBase {actual.YBase} did not match {expectedName} {expected.YBase}.");
            if (expected.IsAlternateBuffer != actual.IsAlternateBuffer)
                mismatches.Add($"{actualName}: alternate buffer {actual.IsAlternateBuffer} did not match {expectedName} {expected.IsAlternateBuffer}.");
            if (expected.VisibleRows.Count != actual.VisibleRows.Count)
                mismatches.Add($"{actualName}: visible row count {actual.VisibleRows.Count} did not match {expectedName} {expected.VisibleRows.Count}.");

            int count = Math.Min(expected.VisibleRows.Count, actual.VisibleRows.Count);
            for (int i = 0; i < count; i++)
            {
                TerminalRowSnapshot expectedRow = expected.VisibleRows[i];
                TerminalRowSnapshot actualRow = actual.VisibleRows[i];
                if (!String.Equals(expectedRow.Text, actualRow.Text, StringComparison.Ordinal))
                {
                    mismatches.Add($"{actualName}: row {i} text mismatch. Expected '{expectedRow.Text}', actual '{actualRow.Text}'.");
                    continue;
                }

                int cellCount = Math.Min(expectedRow.Cells.Count, actualRow.Cells.Count);
                for (int cell = 0; cell < cellCount; cell++)
                {
                    TerminalCellSnapshot expectedCell = expectedRow.Cells[cell];
                    TerminalCellSnapshot actualCell = actualRow.Cells[cell];
                    if (expectedCell.Text != actualCell.Text || expectedCell.Width != actualCell.Width || expectedCell.Attributes != actualCell.Attributes)
                    {
                        mismatches.Add($"{actualName}: row {i}, cell {cell} mismatch.");
                        break;
                    }
                }
            }
        }

        private static IReadOnlyList<string> FindWarnings(TerminalSnapshot snapshot)
        {
            var warnings = new List<string>();
            for (int row = 0; row < snapshot.VisibleRows.Count; row++)
            {
                string text = snapshot.VisibleRows[row].Text;
                if (text.IndexOf('\u001b') >= 0)
                    warnings.Add($"Row {row} contains a literal ESC character.");
                if (text.Contains("Workkingg", StringComparison.Ordinal))
                    warnings.Add($"Row {row} contains duplicated status text: {text}");
            }

            return warnings;
        }

        private static string BuildReport(
            PtyRecordingFixture fixture,
            TerminalSnapshot snapshot,
            IReadOnlyList<string> mismatches,
            IReadOnlyList<string> warnings)
        {
            var builder = new StringBuilder();
            builder.AppendLine("PTY recording replay analysis");
            builder.AppendLine("Recording: " + fixture.RecordingPath);
            builder.AppendLine("Metadata: " + (fixture.MetadataPath ?? "(not found; environment overrides used)"));
            builder.AppendLine($"Size: {snapshot.Columns}x{snapshot.Rows}; Cursor: ({snapshot.CursorColumn},{snapshot.CursorRow}); ViewportY: {snapshot.ViewportY}; YBase: {snapshot.YBase}; Alternate: {snapshot.IsAlternateBuffer}");
            builder.AppendLine($"Bytes: {fixture.Bytes.Length}; Captured chunks: {fixture.CapturedChunks.Count}");
            builder.AppendLine();

            builder.AppendLine("Determinism");
            if (mismatches.Count == 0)
            {
                builder.AppendLine("  PASS: captured, single-chunk, single-byte, and seeded chunk replays matched.");
            }
            else
            {
                foreach (string mismatch in mismatches)
                    builder.AppendLine("  FAIL: " + mismatch);
            }

            builder.AppendLine();
            builder.AppendLine("Warnings");
            if (warnings.Count == 0)
            {
                builder.AppendLine("  None.");
            }
            else
            {
                foreach (string warning in warnings)
                    builder.AppendLine("  " + warning);
            }

            builder.AppendLine();
            builder.AppendLine("Visible rows");
            for (int i = 0; i < snapshot.VisibleRows.Count; i++)
            {
                builder.Append(i.ToString(CultureInfo.InvariantCulture).PadLeft(3, ' '));
                builder.Append(": ");
                builder.AppendLine(snapshot.VisibleRows[i].Text);
            }

            return builder.ToString();
        }

        private static IReadOnlyList<byte[]> SplitEveryByte(byte[] bytes)
        {
            var chunks = new byte[bytes.Length][];
            for (int i = 0; i < bytes.Length; i++)
                chunks[i] = new[] { bytes[i] };

            return chunks;
        }

        private static IReadOnlyList<byte[]> SplitSeeded(byte[] bytes, int seed)
        {
            var chunks = new List<byte[]>();
            var random = new Random(seed);
            int offset = 0;
            while (offset < bytes.Length)
            {
                int remaining = bytes.Length - offset;
                int size = Math.Min(remaining, random.Next(1, Math.Min(remaining, 8192) + 1));
                var chunk = new byte[size];
                Buffer.BlockCopy(bytes, offset, chunk, 0, size);
                chunks.Add(chunk);
                offset += size;
            }

            return chunks;
        }
    }
}
