# Analyze Terminal Output

This workflow turns a visual terminal issue into deterministic replay coverage.
The goal is to prove whether the byte stream produces the wrong XTerm.NET cell
grid, before debugging Avalonia rendering or row caches.

## Capture Raw PTY Output

1. Open the Termrig profile or tab editor.
2. Enable **Record raw PTY output**.
3. Set the recording directory. If the directory is blank, the editor will fill in
   the default `pty-recordings` directory under the local application data folder.
4. Open a new terminal tab using that profile.
5. Reproduce the terminal problem with the smallest command sequence possible.
6. Close the tab or let the process exit so the recording metadata is flushed.

Each capture writes:

- `*.pty.bin`: raw bytes read from the PTY.
- `*.pty.json`: deterministic replay metadata, including terminal size,
  emulator options, process name, OS details, and original PTY read chunk sizes.

Keep the `.pty.bin` and `.pty.json` files together.

## Analyze A Single Capture

From the repository root:

```powershell
cd C:\Code\Termrig\Termrig
$env:TERMRIG_PTY_RECORDING = "C:\path\to\capture.pty.bin"
dotnet test src\Test.Terminal\Test.Terminal.csproj `
  --filter FullyQualifiedName~PtyRecordingAnalysisTests.RecordingFromEnvironmentReplaysDeterministically `
  --logger "console;verbosity=detailed"
```

You can also point `TERMRIG_PTY_RECORDING` at the `.pty.json` sidecar.

The test replays the same bytes four ways:

- original captured chunk sizes
- one full chunk
- one byte per chunk
- deterministic seeded chunks

All four snapshots must match. If they do not, the parser/decoder/emulator path is
chunk-sensitive and not deterministic.

## Write A Snapshot Or Report

To save the replayed terminal cell-grid snapshot:

```powershell
$env:TERMRIG_PTY_ANALYSIS_WRITE_SNAPSHOT = "C:\path\to\capture.pty.expected.json"
```

To save the text report:

```powershell
$env:TERMRIG_PTY_ANALYSIS_WRITE_REPORT = "C:\path\to\capture.report.txt"
```

Then rerun the single-capture test.

## Add A Checked-In Regression Fixture

Create a small fixture folder:

```powershell
cd C:\Code\Termrig\Termrig
New-Item -ItemType Directory -Force src\Test.Terminal\Fixtures\PtyRecordings\my-case
Copy-Item C:\path\to\capture.pty.bin src\Test.Terminal\Fixtures\PtyRecordings\my-case\
Copy-Item C:\path\to\capture.pty.json src\Test.Terminal\Fixtures\PtyRecordings\my-case\
```

If the expected snapshot has been reviewed against a known-good terminal, place it
beside the capture as:

```text
capture.pty.expected.json
```

Run checked-in fixture replay:

```powershell
dotnet test src\Test.Terminal\Test.Terminal.csproj `
  --filter FullyQualifiedName~PtyRecordingAnalysisTests.CheckedInPtyRecordingFixturesReplayDeterministically
```

The fixture test always verifies deterministic replay. If an expected snapshot file
exists, it also verifies the replayed XTerm.NET cell grid against that snapshot.

## Legacy Recordings Without Metadata

Older `.pty.bin` files can still be replayed, but the terminal size must be supplied:

```powershell
$env:TERMRIG_PTY_RECORDING = "C:\path\to\old-capture.pty.bin"
$env:TERMRIG_PTY_COLUMNS = "120"
$env:TERMRIG_PTY_ROWS = "30"
$env:TERMRIG_PTY_CONVERT_EOL = "false"
$env:TERMRIG_PTY_TRIM_LINE_ENDING_PADDING = "true"
$env:TERMRIG_PTY_PIN_VIEWPORT_TO_BOTTOM = "true"
dotnet test src\Test.Terminal\Test.Terminal.csproj `
  --filter FullyQualifiedName~PtyRecordingAnalysisTests.RecordingFromEnvironmentReplaysDeterministically
```

Prefer new captures with sidecar metadata when possible.

## Determinism And Correctness

Determinism means the same bytes and metadata always produce the same terminal
cell grid regardless of read chunking. This catches split UTF-8, parser state, and
incremental write bugs.

Correctness means the resulting cell grid matches what a real terminal should show.
For correctness, compare the generated snapshot with a known-good terminal or a
manually reviewed cell-grid expectation, then commit the `.pty.expected.json`
snapshot with the fixture.

Use this order when investigating:

1. Replay the recording and check determinism.
2. Compare the XTerm.NET snapshot to a known-good expected snapshot.
3. If XTerm.NET is wrong, patch the vendored emulator and keep the fixture.
4. If XTerm.NET is correct but Termrig's UI is wrong, debug Avalonia rendering,
   row cache invalidation, or scroll rendering.
