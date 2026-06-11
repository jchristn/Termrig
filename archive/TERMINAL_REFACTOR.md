# Terminal Refactor Plan

## Agreed Position

TermRig's terminal fidelity problems are primarily in the host integration around XTerm.NET, not proven defects in the VT parser itself. The refactor should keep XTerm.NET as the emulator until replay fixtures prove otherwise.

The core work is:

- Make render/write access to terminal state concurrency-safe.
- Add row-granular damage/version tracking so cached render output cannot go stale.
- Remove byte and geometry mutations that alter the terminal stream or lie to terminal applications.
- Replace reflection/timer workarounds with public terminal-control APIs.
- Define PTY byte policy, startup sizing, resize behavior, and environment defaults explicitly.

Emulator replacement is a contingency only after the harness proves XTerm.NET is the failing layer.

## Progress Legend

Use these markers while implementing:

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete
- `[!]` Blocked, with note

## Phase 0 - Baseline And Guardrails

- [x] Record current branch, commit, and uncommitted changes before editing.
- [x] Identify existing local patches in `src/ThirdParty/Iciclecreek.Avalonia.Terminal`.
- [x] Confirm current terminal package/source ownership: vendored fork, upstream package, or both.
- [x] Decide where refactor tests should live.
  - Suggested: a dedicated terminal test project under `src` or `tests`.
- [x] Add this file to normal project tracking and update it as work lands.

Completion notes:

- Owner: Codex
- Date: 2026-05-31
- PR/commit: working tree on `fix/terminal` at baseline `71f20d2808b4e97af575269700d627d1b497db30`
- Remaining risks: pre-existing uncommitted workspace close confirmation files and edits were preserved; terminal package ownership is a vendored Avalonia integration plus upstream XTerm.NET package reference.

## Phase 1 - Replay Harness First

Goal: make terminal fidelity testable before changing behavior.

- [~] Add a binary replay fixture format for raw PTY output bytes.
- [x] Add a replay runner that writes bytes into XTerm.NET at fixed terminal sizes.
- [x] Capture snapshots from the terminal model after replay.
- [ ] Snapshot at minimum:
  - [x] visible cell text
  - [~] cell attributes/colors
  - [~] cursor position and style
  - [x] normal vs alternate buffer state
  - [~] scrollback contents and position
  - [x] terminal dimensions
- [x] Add split UTF-8 replay coverage where multi-byte characters cross chunk boundaries.
- [ ] Add burst-output replay coverage to catch concurrent mutation/render hazards.
- [ ] Add fixtures for:
  - [ ] `cmd.exe` prompt, command echo, Up-arrow recall
  - [ ] PowerShell plus PSReadLine editing
  - [ ] bash prompt and readline editing
  - [x] Docker/progress-style carriage-return updates
  - [ ] Claude Code or Codex-style screen rewrites
  - [ ] colored output with 16-color, 256-color, and truecolor SGR
  - [ ] Unicode, box drawing, combining characters, and wide glyphs
  - [ ] a full-screen alternate-buffer app, such as `htop`, `vim`, or equivalent
- [~] Document how to record new fixtures.
- [x] Make replay tests runnable from the repo's normal test command.

Acceptance criteria:

- [ ] A developer can add a raw byte fixture and an expected terminal-state snapshot without launching the UI.
- [ ] At least one fixture fails or documents current broken behavior before fixes begin.
- [ ] The harness can run deterministically in CI or a local non-interactive test run.

Completion notes:

- Owner:
- Date:
- PR/commit:
- Fixture location: `src/Test.Terminal`
- Test command: `dotnet test src/Termrig.slnx --no-build`

## Phase 2 - Render/Write Concurrency Safety

Goal: prevent torn reads while the PTY reader mutates terminal state.

Known issue:

- `ReadPtyOutputAsync` writes to `_terminal` under `_terminalLock`.
- `Render` reads `_terminal.Buffer` without the same lock.
- `_terminal.Buffer.ScrollToBottom()` is called from the background reader path outside the lock.

Tasks:

- [~] Audit every read and write of `_terminal`, `_terminal.Buffer`, buffer lines, scrollback, cursor, and selection.
- [x] Choose the synchronization model:
  - [ ] Option A: render snapshots terminal state/damage under lock, then draws from the snapshot.
  - [x] Option B: `Render` holds `_terminalLock` while reading/drawing terminal state.
  - [ ] Option C: finer-grained damage/snapshot lock, if justified by performance data.
- [x] Move background-thread `ScrollToBottom()` access under the same synchronization model.
- [~] Ensure UI-thread input, paste, resize, kill, and process lifecycle operations obey the same ownership rules.
- [ ] Add a burst-output test that would previously expose torn buffer reads.
- [ ] Measure render latency and lock hold time with a large output burst.

Acceptance criteria:

- [ ] No terminal buffer state is read by `Render` without the chosen synchronization guard.
- [ ] No terminal buffer state is mutated outside the chosen synchronization guard.
- [ ] Burst-output replay is stable across repeated runs.
- [ ] Interactive typing remains responsive under sustained output.

Completion notes:

- Owner:
- Date:
- PR/commit:
- Synchronization model chosen: `Render` and terminal mutation paths use `_terminalLock`; public selection/paste/cache APIs avoid workspace reflection.
- Performance notes: no latency instrumentation yet; solution tests pass.

## Phase 3 - Row Damage And Cache Invalidation

Goal: make cached render runs impossible to reuse after the underlying row changes.

Known issue:

- `RenderNormalLine()` reuses `line.Cache` and returns without re-inspecting cells.
- Cache invalidation is not tied to row mutation.
- Existing `BufferChanged` notification only indicates normal vs alternate buffer, not row damage.

Tasks:

- [ ] Locate all places where `BufferLine` content or attributes can change.
- [~] Add row versioning or dirty-row tracking.
  - [ ] Prefer a TermRig-owned adapter if it can observe all mutations correctly.
  - [ ] Patch the vendored XTerm.NET/Iciclecreek layer if mutation visibility requires it.
- [x] Invalidate `line.Cache` when row text or attributes change.
- [ ] Include erase operations, cursor-addressed rewrites, scroll-region operations, insert/delete line, wrapping, resize, and alternate-buffer switches.
- [ ] Ensure reverse video, selection, cursor blink, and double-width fallback still invalidate correctly.
- [x] Remove any reliance on periodic cache clearing for correctness.
- [~] Add replay tests for CR redraws, CUP/CHA row rewrites, EL/ED erases, and progress bars.

Acceptance criteria:

- [ ] A row cache is reused only when row content, attributes, and relevant render modes are unchanged.
- [ ] Docker/progress-style updates no longer duplicate or commingle stale text in replay.
- [ ] Cursor-addressed rewrites update existing rows instead of preserving old cached output.
- [ ] Cache invalidation is deterministic and test-covered.

Completion notes:

- Owner:
- Date:
- PR/commit:
- Damage tracking design: cached line renders carry a content/attribute fingerprint and are reused only when the fingerprint still matches.
- Known limitations: this is deterministic cache validation rather than true row-granular mutation versioning inside XTerm.NET. Follow-up Docker/progress reports exposed XTerm.NET integration issues around stale rendered pixels on shorter rewrites and Docker's cursor-padding sequences. Normal row rendering now clears the full row before drawing cached or rebuilt runs. PTY output statefully trims trailing spaces immediately before line endings, preserves those pending spaces across SGR resets, converts Docker-style `CSI n X` erase padding to `CSI K`, and converts `CSI n C` cursor-forward padding to absolute `CSI G` positioning so XTerm.NET cannot wrap while padding rows. The small status-column padding conversion has a captured regression for `Network docker_default              Created` spacing.

## Phase 4 - Remove Byte And Geometry Mutations

Goal: stop altering the application stream and terminal dimensions to hide rendering bugs.

Tasks:

- [x] Delete `NormalizeCarriageReturnRedraws(...)`.
- [x] Remove any injection of `ESC[2K` or other escape sequences into PTY output.
- [x] Delete the `GetPtyColumns(cols - 1)` behavior.
- [x] Ensure PTY-reported columns exactly match rendered terminal columns.
- [x] Replace per-chunk `Encoding.UTF8.GetString(buffer, 0, bytesRead)` with a stateful `System.Text.Decoder`.
- [x] Add tests for multi-byte UTF-8 split across reads.
- [~] Audit for other output transformations, line-ending conversions, or app-specific output modes.
- [~] Decide and document `ConvertEol` as a PTY-layer byte-policy decision.
  - [x] Legacy ConPTY:
  - [ ] ConPTY pass-through mode:
  - [x] Unix/raw PTY:
- [~] Add replay fixtures proving partial-line CR updates, full-width lines, and split Unicode behave correctly.

Acceptance criteria:

- [ ] Raw PTY output is not rewritten by the renderer or workspace layer.
- [ ] PTY geometry matches the visual grid without a one-column lie.
- [ ] Split UTF-8 sequences decode correctly.
- [ ] `ConvertEol` has a single documented owner and no uncoordinated overrides.

Completion notes:

- Owner:
- Date:
- PR/commit:
- Byte policy decision: renderer/workspace no longer injects erase sequences into raw PTY output. Windows ConPTY uses `ConvertEol = false` so the PTY byte stream owns line endings; Unix/raw PTY keeps `ConvertEol = true` for LF-only output. A stateful compatibility shim trims trailing spaces directly before CR/LF and normalizes Docker-style `CSI n X`/`CSI n C` padding because XTerm.NET wraps while processing these full-width rows.
- Fixtures added: split UTF-8, carriage-return redraw, cursor-addressed rewrite, cursor-up progress rewrite.

## Phase 5 - Public TerminalControl Boundary

Goal: remove reflection and app-layer terminal internals access.

Known issue:

- `TerminalWorkspaceWindow` reaches private terminal internals by reflection for paste, startup command injection, kill, cache clearing, and invalidation.
- `TerminalControl` exposes raw `XTerm.Terminal`, making layer boundaries unenforceable.

Tasks:

- [x] Inventory every reflection access from `TerminalWorkspaceWindow`.
- [x] Add public APIs to `TerminalControl` for required operations:
  - [x] paste/bracketed paste
  - [x] send text/input
  - [ ] startup command injection, if still needed
  - [x] kill/terminate session
  - [ ] resize
  - [ ] focus
  - [x] current directory or shell integration metadata
  - [ ] capture replay/debug data
  - [x] invalidate/render request, if still needed
- [x] Remove the 33ms timer that clears caches.
- [x] Remove reflection-based cache clearing and invalidation.
- [x] Stop exposing raw `XTerm.Terminal` publicly, or mark it internal/obsolete with a migration path.
- [x] Keep workspace responsibilities limited to tabs, profiles, layout, focus, and high-level commands.

Acceptance criteria:

- [ ] No reflection is used to control terminal internals from workspace code.
- [ ] No periodic cache-clearing timer is required for correctness.
- [ ] Workspace code depends only on public terminal-control APIs.
- [ ] Terminal internals are not exposed as mutable public state.

Completion notes:

- Owner:
- Date:
- PR/commit:
- APIs added: `PasteAsync`, `PasteTextAsync`, `SendTextAsync`, `HasSelection`, `GetSelectedText`, `ClearSelection`, `ClearVisibleLineCaches`, `RequestRenderInvalidate`, `TerminateSession`.
- APIs removed/obsolete: `TerminalControl.Terminal` and `TerminalView.Terminal` are marked obsolete; workspace reflection use was removed.

## Phase 6 - Startup Size And Resize Contract

Goal: ensure shell, PTY, emulator, and renderer agree on dimensions from startup onward.

Tasks:

- [ ] Prevent launching the shell before the control has a real measured row/column size.
- [ ] Define minimum fallback dimensions if layout cannot produce a size.
- [~] Ensure initial PTY size, emulator size, and rendered grid size are identical.
- [ ] Debounce resize events so PTY and emulator do not disagree mid-frame.
- [ ] Preserve user scrollback position correctly during resize.
- [ ] Add tests for initial launch size and rapid resize.
- [~] Verify full-width lines do not wrap early or late.

Acceptance criteria:

- [ ] Initial shell prompt sees the same dimensions the user sees.
- [ ] Resize updates PTY and emulator consistently.
- [ ] There is no N-1 column compensation anywhere.
- [ ] Rapid resize does not corrupt visible rows or scrollback.

Completion notes:

- Owner:
- Date:
- PR/commit:
- Resize policy: PTY resize now receives the exact rendered column count; the previous N-1 compensation was removed.
- Remaining edge cases: launch still uses current terminal dimensions at process start; explicit deferred launch-until-measured behavior and rapid-resize tests remain.

## Phase 7 - PTY And Environment Policy

Goal: make terminal mode and advertised capabilities explicit.

Tasks:

- [~] Set and document default `TERM`.
  - Recommended starting point: `xterm-256color`.
- [ ] Set and document default `COLORTERM`.
  - Recommended starting point: `truecolor`.
- [ ] Evaluate ConPTY pass-through availability on supported Windows versions.
- [ ] Choose behavior when pass-through is unavailable.
- [~] Document how ConPTY mode affects `ConvertEol`.
- [ ] Audit mouse mode, bracketed paste, focus events, and modifyOtherKeys behavior.
- [ ] Add settings or feature flags only where users need compatibility escape hatches.
- [ ] Add diagnostics that report PTY mode, terminal size, env policy, and byte policy.

Acceptance criteria:

- [ ] TermRig advertises capabilities that match what it actually supports.
- [ ] PTY mode and byte policy are visible in diagnostics.
- [ ] Windows and Unix behavior are documented separately where they differ.
- [ ] Compatibility fallbacks are intentional, not accidental hacks.

Completion notes:

- Owner:
- Date:
- PR/commit:
- Windows policy: `TermName` remains `xterm-256color`; `ConvertEol` is false for ConPTY output; ConPTY pass-through policy still open.
- Unix policy: existing non-Windows `ConvertEol` handling remains true for LF-only raw PTY output.

## Phase 8 - Emulator Contingency

Goal: patch or replace XTerm.NET only if tests prove emulator-side defects remain.

Trigger criteria:

- [ ] Replay tests still fail after Phases 2 through 7.
- [ ] Failure is reproduced at the XTerm.NET terminal-state level, independent of Avalonia rendering.
- [ ] Failure cannot be explained by PTY policy, input translation, sizing, cache invalidation, or concurrency.

Options:

- [ ] Patch vendored XTerm.NET/Iciclecreek code.
- [ ] Maintain a TermRig-owned adapter/fork with tests.
- [ ] Evaluate replacement cores only with proof that they support required VT/xterm behavior and Avalonia integration.

Acceptance criteria:

- [ ] Any emulator patch has a replay fixture that fails before and passes after.
- [ ] Replacement is not started without a written decision record.
- [ ] Decision includes integration cost, maintenance cost, VT coverage, Unicode support, and Windows behavior.

Completion notes:

- Owner:
- Date:
- Decision:
- PR/commit:

## Definition Of Done

The refactor is complete when:

- [~] Replay harness exists and covers the major failure classes.
- [~] Render/write terminal-state access is concurrency-safe.
- [~] Row cache invalidation is tied to terminal-state mutation.
- [x] Raw PTY output is not rewritten by terminal rendering code.
- [x] PTY-reported dimensions match rendered dimensions.
- [x] UTF-8 decoding is stateful across PTY reads.
- [~] `ConvertEol` is owned by the PTY byte-policy layer.
- [x] Reflection-based terminal control is removed from workspace code.
- [~] Startup and resize sizing are deterministic.
- [ ] `TERM`, `COLORTERM`, and ConPTY policy are documented.
- [x] All new replay tests pass.
- [ ] Manual smoke testing passes for cmd, PowerShell, bash, Docker/progress output, Codex/Claude-style rewrites, Unicode, colors, and an alternate-screen TUI.

## Manual Smoke Test Matrix

Record result as Pass/Fail plus notes.

| Scenario | Size | Result | Notes |
| --- | --- | --- | --- |
| `cmd.exe` prompt, command echo, history recall | 80x24 |  |  |
| PowerShell PSReadLine editing and history recall | 80x24 |  |  |
| bash readline editing | 80x24 |  |  |
| Docker/progress carriage-return updates | 120x30 |  |  |
| Claude Code or Codex-style rewrite-heavy session | 120x30 |  |  |
| 16-color, 256-color, and truecolor SGR output | 120x30 |  |  |
| Unicode box drawing, combining marks, wide glyphs | 120x30 |  |  |
| Alternate-screen TUI enter/exit | 120x30 |  |  |
| Rapid resize during output burst | varied |  |  |
| Scrollback after long output | 120x30 |  |  |

## Open Questions

- [x] Which test project should own replay fixtures?
- [x] Should render use full-lock drawing first, then optimize to snapshots later?
- [~] Can damage tracking be implemented cleanly without patching XTerm.NET internals?
- [ ] Which Windows versions should enable ConPTY pass-through by default?
- [ ] What user-facing compatibility settings are required, if any?
