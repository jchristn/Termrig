# Termrig Terminal Output Fix Report

## Agreed Position

The corruption shown in `1.png` is stale cell content created before the affected rows enter scrollback. It is not a renderer/cache bug and not a scrollback copying bug. `TerminalBuffer.ScrollUp` faithfully advances existing `BufferLine` objects into scrollback; if a row already contains stale right-hand cells, scrolling preserves that stale content.

The direct fault line is `LineEndingPaddingNormalizer`, a non-terminal byte-stream transformer applied to live PTY output before XTerm.NET parses it. The normalizer deletes, delays, and rewrites terminal-significant bytes and control sequences. When Codex/Claude redraw shorter rows or reposition text, Termrig can fail to clear or place cells exactly as a real terminal would, leaving old right-hand text in the grid. Those already-corrupted rows are then carried upward as normal output scrolls.

The targeted fix is:

1. Add explicit pending-wrap state to XTerm.NET instead of using `X >= Cols` as an implicit sentinel.
2. Remove/bypass `LineEndingPaddingNormalizer` from the live PTY path and from normal replay once pending-wrap behavior is correct.
3. Add captured `.pty.bin` fixtures for the failing Codex/Claude case and for the ConPTY/Docker full-width padding case, then assert expected cell-grid snapshots.

Do not ship the standalone change "flush pending spaces before CR/LF" as the whole fix. It repairs one real lossy transform, but it does not address the normalizer's `CUF`, `ECH`, and SGR-adjacent transforms, and it may reintroduce the ConPTY/Docker extra-row regression that the normalizer was added to hide.

## Evidence From Source

### Live PTY output is mutated before parsing

- `TerminalView.ReadPtyOutputAsync` creates a `LineEndingPaddingNormalizer` unconditionally: `src/ThirdParty/Iciclecreek.Avalonia.Terminal/TerminalView.cs:2340`.
- The raw bytes are recorded before normalization: `TerminalView.cs:2369-2372`.
- The decoded text is normalized before it reaches XTerm.NET: `TerminalView.cs:2381-2382`.
- The terminal receives only the normalized text: `TerminalView.cs:2397`.
- Recording metadata marks live captures as using the normalizer: `TerminalView.cs:2487`.

This means a raw `.pty.bin` capture can be replayed both ways: with the current normalized behavior and with raw terminal semantics.

### The normalizer is lossy

In both the live copy and test replay copy:

- Literal spaces are buffered instead of emitted immediately: `TerminalView.cs:148-151`, `src/Test.Terminal/TerminalReplay.cs:167-170`.
- Pending spaces are discarded when the next byte is `\r` or `\n`: `TerminalView.cs:154-157`, `TerminalReplay.cs:173-176`.
- `CSI ... m` SGR sequences are emitted without first flushing pending spaces, so spaces can move across attribute changes: `TerminalView.cs:177-181`, `TerminalReplay.cs:196-200`.
- Large `CSI n C` cursor-forward commands are rewritten into absolute `CSI n G` based on the normalizer's partial `_column` model: `TerminalView.cs:184-193`, `TerminalReplay.cs:203-213`.
- Large `CSI n X` erase-character commands are rewritten to `CSI K`, which erases to end of line instead of erasing exactly `n` cells: `TerminalView.cs:196-199`, `TerminalReplay.cs:215-218`.

Those transforms are terminal-fidelity bugs. They are especially risky for full-screen tools that redraw rows frequently, including Codex and Claude Code.

### Scrolling preserves the bad cells; it does not create them

- `TerminalBuffer.ScrollUp` creates one blank line, pushes/splices it into the buffer, and adjusts `yBase`/`yDisp`: `src/ThirdParty/XTerm.NET/Buffer/TerminalBuffer.cs:136-194`.
- It does not synthesize repeated text. Stale text visible after scrolling was already in the row's cells before scrollback promotion.

### Rendering cache is not the shown corruption

- `RenderNormalLine` computes a line fingerprint before using a cached render: `TerminalView.cs:2690-2697`.
- The fingerprint includes terminal columns, line length, line attributes, cursor blink state, each cell width, each cell attributes, and each cell content: `TerminalView.cs:2793-2817`.
- A changed cell forces a cache miss. Therefore the repeated phrase in `1.png` is real model data, not a stale paint artifact.

### Existing tests show why the workaround exists, but not the failing Codex fixture

- Docker/ConPTY full-width row tests currently depend on `trimLineEndingPadding: true`: `src/Test.Terminal/TerminalReplayTests.cs:67-126`.
- The synthetic agent-shaped test uses `CSI 2 K`, carriage return, and small `CSI 4 C`; it checks for duplicated status text: `TerminalReplayTests.cs:140-151`.
- `src/Test.Terminal/Fixtures/PtyRecordings` currently contains only `README.md`; there is no checked-in failing Codex/Claude `.pty.bin` fixture yet.

## Why This Happens

XTerm.NET models pending autowrap by allowing the cursor column to become one past the last terminal column:

- Autowrap checks `_buffer.X >= _terminal.Cols`: `src/ThirdParty/XTerm.NET/InputHandler.cs:121-141`.
- Printable output advances with `SetCursorRaw(_buffer.X + width, _buffer.Y)`: `InputHandler.cs:189-190`.
- Normal cursor movement clamps with `SetCursor`: `src/ThirdParty/XTerm.NET/Buffer/TerminalBuffer.cs:339-342`.
- `SetCursorRaw` stores the raw value without clamping: `TerminalBuffer.cs:348-351`.

That conflates actual cursor position with "wrap is pending." When ConPTY or Docker emits full-width padded lines, this implicit sentinel can create extra rows around `CRLF`. `LineEndingPaddingNormalizer` was introduced as a byte-stream workaround: strip or rewrite the padding so the parser never reaches the problematic edge case.

The workaround creates the Codex/Claude class of bug. Agents and TUIs rely on exact terminal semantics for spaces, erase commands, cursor movement, and SGR-separated text. Because the normalizer mutates those bytes before parsing, XTerm.NET can leave stale cells at the right side of a row or place new content in the wrong cells. As output scrolls, those rows move upward with the stale cells still present.

The exact transform that fired in `1.png` must be confirmed with a captured fixture. The source proves the fault line and the failure mechanism; the capture identifies whether the visible screenshot was triggered by dropped trailing spaces, `CUF` rewrite, `ECH` rewrite, SGR-adjacent delayed spaces, or a combination.

## Required Implementation Plan

Use this as the execution checklist.

### 1. Capture fixtures

- [ ] Reproduce the `1.png` Codex/Claude corruption with PTY recording enabled in the relevant Termrig tab profile.
- [ ] Save the resulting `.pty.bin` and `.pty.json` under `src/Test.Terminal/Fixtures/PtyRecordings/`.
- [ ] Minimize the fixture if needed, but preserve the bytes that produce the stale phrase.
- [ ] Capture or construct a ConPTY/Docker full-width padding fixture that currently explains the existing normalizer tests.
- [ ] Generate `.pty.expected.json` snapshots from a known-good terminal interpretation or from Termrig after the fix.

Useful existing hooks:

```powershell
$env:TERMRIG_PTY_RECORDING='C:\path\to\capture.pty.json'
$env:TERMRIG_PTY_ANALYSIS_WRITE_REPORT='C:\temp\pty-report.txt'
$env:TERMRIG_PTY_ANALYSIS_WRITE_SNAPSHOT='C:\temp\pty-snapshot.json'
dotnet test C:\code\termrig\termrig\src\Test.Terminal\Test.Terminal.csproj --filter RecordingFromEnvironmentReplaysDeterministically
```

For checked-in fixtures:

```powershell
$env:TERMRIG_PTY_FIXTURE_DIR='C:\code\termrig\termrig\src\Test.Terminal\Fixtures\PtyRecordings'
dotnet test C:\code\termrig\termrig\src\Test.Terminal\Test.Terminal.csproj --filter CheckedInPtyRecordingFixturesReplayDeterministically
```

### 2. Add raw-vs-normalized replay analysis

- [ ] Add a replay helper that runs the same `PtyRecordingFixture` twice:
  - once with `TrimLineEndingPadding = true`, matching current live metadata;
  - once with `TrimLineEndingPadding = false`, matching real terminal byte semantics.
- [ ] For the Codex/Claude fixture, assert that the raw/fixed path does not contain the stale repeated phrase from `1.png`.
- [ ] For the current code, document which normalizer transform changes the relevant bytes.
- [ ] Keep chunking determinism checks from `PtyRecordingAnalyzer.Analyze`, which already compares captured chunks, single chunk, single-byte chunks, and seeded chunks: `src/Test.Terminal/PtyRecordingReplaySupport.cs:244-256`.

### 3. Implement explicit pending wrap

- [ ] Add explicit pending-wrap state to the terminal buffer or input handler. Do not encode this state as `X == Cols`.
- [ ] When a printable character reaches the last column with wraparound enabled, write the character and set pending-wrap without immediately scrolling or creating a new row.
- [ ] Before the next printable character, if pending-wrap is set, move to column 0 of the next row or scroll if already at the bottom margin, then print.
- [ ] Cancel pending-wrap on cursor movement and control sequences that real terminals use to abandon the pending wrap state, including carriage return and absolute/relative cursor positioning.
- [ ] Add focused tests for:
  - full-width text followed by `\r\n` does not create an extra blank/logical row;
  - full-width text followed by another printable character wraps exactly once;
  - carriage return after a full-width row cancels pending wrap;
  - cursor movement after a full-width row cancels pending wrap;
  - wide characters at the right edge behave correctly.

### 4. Remove the live normalizer

- [ ] Change `TerminalView.ReadPtyOutputAsync` so decoded PTY text is written directly to `_terminal.Write(...)` after recording, not through `LineEndingPaddingNormalizer.Process(...)`.
- [ ] Set new recording metadata to `UsesLineEndingPaddingNormalizer = false`.
- [ ] Remove the duplicate normalizer from normal `TerminalReplay`, or keep it only as an explicitly named legacy replay mode for proving old behavior.
- [ ] Keep old fixtures replayable by honoring their metadata only in legacy tests, not in the live path.
- [ ] Run the Docker/ConPTY tests without trimming. They should pass because pending-wrap is fixed, not because bytes are stripped.
- [ ] Run the Codex/Claude fixture and verify no stale phrase is carried into scrollback.

### 5. Add direct regression tests

- [ ] Add a synthetic stale-tail test:

```text
old stale tail\rnew          \r\n
```

Expected result: the visible row contains `new` with no `stale tail` cells.

- [ ] Add a `CSI n X` test proving erase-character clears exactly `n` cells and does not erase to end of line.
- [ ] Add a large `CSI n C` test proving cursor-forward is interpreted by the parser, not rewritten to absolute cursor position by a pre-parser heuristic.
- [ ] Keep `CursorForwardStatusRewriteKeepsExistingTextInPlace` and make sure it passes without the normalizer.

## Similar Fidelity Risks To Fix After The Main Bug

Track these separately from the stale-tail fix.

- [ ] `CSI 3 J` is currently handled like visible-screen erase. In xterm-style terminals, mode 3 erases scrollback: `src/ThirdParty/XTerm.NET/InputHandler.cs:898-904`.
- [ ] `EraseInLine` fills cells but does not reset `BufferLine.IsWrapped`, which can leave stale wrap metadata after line erases: `InputHandler.cs:908-929`.
- [ ] Resize resizes each line in place and does not reflow wrapped content like xterm-compatible terminals: `src/ThirdParty/XTerm.NET/Buffer/TerminalBuffer.cs:291-333`.
- [ ] `BufferLine.Clone` and `CopyFrom` copy `Cache` by reference. The current fingerprint protects the main paint path, but cache aliasing is still unsafe shared mutable state: `src/ThirdParty/XTerm.NET/Buffer/BufferLine.cs:218-248`.
- [ ] The normalizer's partial `_column` parser ignores wide characters, combining characters, tabs, backspace, origin mode, most CSI commands, and wrap state. Removing the normalizer from the live path eliminates this class of pre-parser drift.

## Acceptance Criteria

- [ ] A checked-in Codex/Claude fixture reproduces the old stale-tail behavior before the fix.
- [ ] The same fixture renders without the repeated stale phrase after the fix.
- [ ] A checked-in ConPTY/Docker fixture renders without extra rows after the fix and without live byte normalization.
- [ ] `dotnet test C:\code\termrig\termrig\src\Test.Terminal\Test.Terminal.csproj` passes.
- [ ] Live Termrig sessions with Codex and Claude Code no longer carry old right-hand row content upward as output scrolls.
