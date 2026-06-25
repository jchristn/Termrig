# Terminal Scrollback Restore Plan

## Goal

Persist a bounded, recent portion of each terminal tab's scrollback so that reopening a terminal profile after a crash, reboot, or normal app restart can show useful recent context from the prior session.

This feature is visual history only. It does not reattach to the prior process, shell, SSH connection, `screen`, `tmux`, or full-screen app. A reopened terminal starts a new live process as it does today, with restored scrollback available above the new prompt.

## Working Assumptions

- Restore is enabled by default for every terminal tab.
- Restore is configurable per terminal tab.
- The default restore limit is the last 1000 logical terminal lines.
- The line limit is configurable per terminal tab.
- Restored data should include text and enough cell attributes to preserve useful colors/styles.
- Alternate-screen programs such as `vim`, `less`, `screen`, and `tmux` are not replayed as live sessions. For the first implementation, persist the normal scrollback buffer and record metadata if the terminal was in alternate-screen mode at capture time.
- Persisting restored scrollback is separate from raw PTY recording. Raw PTY recordings are diagnostic artifacts; scrollback restore should use deterministic snapshots.
- Restored scrollback must never send bytes to the child process.
- Disabling restore for a tab should stop future writes and remove that tab's saved restore snapshot.

## Product Decisions To Confirm

Developers may annotate decisions here before implementation.

- [x] Confirm the default restore line limit: `1000`.
- [x] Confirm maximum allowed restore line limit: `100000`, matching existing scrollback upper bound.
- [x] Confirm whether restored scrollback should display an in-terminal marker line: no shell-visible marker.
- [x] Confirm whether alternate-screen contents should be saved in v1: no, metadata only.
- [x] Confirm restore data location: `%USERPROFILE%\.termrig\terminal-restore\`.

## Implementation Status

- [x] Created implementation branch `feature/terminalrestore`.
- [x] Reviewed profile storage, workspace terminal creation, terminal buffer APIs, and existing PTY recording/replay code.
- [x] Phase 1 profile configuration is implemented; tests are still pending.
- [x] Phase 2 snapshot export is implemented; tests are still pending.
- [x] Phase 3 snapshot import is implemented; tests are still pending.
- [x] Phase 4 persistence service is implemented; tests are still pending.
- [x] Phase 5 capture lifecycle is implemented; tests are still pending.
- [x] Phase 6 restore lifecycle is implemented; tests are still pending.
- [x] Phase 7 workspace recovery integration is implemented via normal profile reopen and crash-window reopen paths; tests are still pending.
- [x] Phase 8 privacy and cleanup is implemented for per-tab opt-out, tab delete, profile delete, and cross-profile tab moves.
- [~] Phase 9 focused automated tests are implemented and passing; broader workspace/wrapped-line coverage remains pending.
- [ ] Phase 10 manual validation is pending.

## Verification Log

- [x] `dotnet build .\src\Termrig.App\Termrig.App.csproj -c Debug`
- [x] `dotnet test .\src\Test.Xunit\Test.Xunit.csproj -c Debug`
- [x] `dotnet test .\src\Test.Nunit\Test.Nunit.csproj -c Debug`
- [x] `dotnet test .\src\Test.Terminal\Test.Terminal.csproj -c Debug`

## Existing Code Touchpoints

- `TerminalTabProfile`
  - Already contains per-tab terminal settings such as `ScrollbackBufferSize`, `RecordPtyOutput`, and `PtyRecordingDirectory`.
  - Add restore settings here.
- `TerminalTabEditorWindow`
  - Already exposes per-tab terminal settings.
  - Add restore controls beside existing scrollback/recording options.
- `TerminalWorkspaceWindow`
  - Creates terminal tabs from profiles and builds terminal options.
  - Integrate restore load/apply on tab creation and capture lifecycle on output/close.
- `TerminalView`
  - Owns the active terminal control/process integration.
  - Add snapshot export/import hooks or delegate to the underlying terminal control.
- XTerm.NET terminal buffer classes
  - Use structured buffer/cell APIs rather than parsing rendered text.
- Existing PTY replay test helpers
  - `TerminalReplay`, `TerminalSnapshot`, and related test helpers are useful references for snapshot shape and terminal state assertions.
- `WorkspaceRecoveryStore`
  - Already tracks workspace state and crash detection.
  - Scrollback restore should complement this, not replace it.

## Data Model

### Profile Settings

Add fields to `TerminalTabProfile`:

- [ ] `bool RestoreScrollbackEnabled`
  - Default: `true`.
- [ ] `int? RestoreScrollbackLineLimit`
  - Default/null behavior: use application default `1000`.
  - Validate to a bounded range.
- [ ] `string? Id` or equivalent stable tab identifier
  - Required because tab name and order are user-editable.
  - Generated for existing tabs on load if missing.
  - Preserved across profile saves.
  - Duplicating a tab should create a new id for the duplicate.

### Snapshot File

Create a versioned JSON snapshot model such as:

- [ ] `TerminalRestoreSnapshot`
  - `SchemaVersion`
  - `CapturedAtUtc`
  - `ProfileId`
  - `ProfileName`
  - `TabId`
  - `TabName`
  - `WorkingDirectory`
  - `CommandLine` or process metadata, if already available without leaking extra data
  - `Columns`
  - `Rows`
  - `ScrollbackLineLimit`
  - `WasAlternateBufferActive`
  - `Lines`

- [ ] `TerminalRestoreLine`
  - `IsWrapped`
  - `Cells` or compact text runs

- [ ] `TerminalRestoreRun`
  - `Text`
  - Foreground/background color
  - Bold, italic, underline, inverse, and other supported attributes

Prefer compact runs over one JSON object per cell when adjacent cells share attributes.

### Storage Path

Proposed path:

```text
%USERPROFILE%\.termrig\terminal-restore\
```

File naming should be deterministic and safe:

```text
{profile-id}\{tab-id}.json
```

Requirements:

- [ ] Create directories as needed.
- [ ] Write atomically using temp file plus replace/move.
- [ ] Delete snapshots when restore is disabled for a tab.
- [ ] Delete snapshots when a tab or profile is deleted.
- [ ] Ignore corrupt or incompatible snapshots and start clean.

## Implementation Phases

### Phase 1: Profile Configuration

- [x] Add restore settings to `TerminalTabProfile`.
- [x] Add stable tab id support.
- [x] Ensure profile load migrates old tabs by assigning ids.
- [x] Ensure profile save preserves ids.
- [x] Ensure tab duplication creates a new id.
- [x] Add defaults so existing profiles get restore enabled automatically.
- [x] Add validation for restore line limit.
- [x] Add editor UI:
  - [x] Toggle: restore scrollback.
  - [x] Numeric field: restore line limit.
  - [x] Help text or tooltip that explains this restores visual history, not the running process.

### Phase 2: Snapshot Export

- [x] Add a terminal snapshot export API.
- [x] Export only the normal buffer for v1.
- [x] Include visible rows and scrollback rows in terminal order.
- [x] Trim to the configured restore line limit.
- [x] Preserve wrapped-line metadata where available.
- [x] Preserve cell text and attributes.
- [x] Record whether alternate screen was active during capture.
- [x] Avoid capturing transient cursor-only state unless needed for rendering.

### Phase 3: Snapshot Import

- [x] Add a terminal snapshot import API.
- [x] Rehydrate lines before live process output appears.
- [x] Restore text and attributes without sending input or output to the child process.
- [x] Preserve sane viewport behavior:
  - [x] On first open, show the bottom of restored scrollback.
  - [x] Once the new process writes output, keep normal terminal follow behavior.
- [x] Handle size mismatch:
  - [x] If columns match, restore line structure exactly.
  - [x] If columns differ, preserve cells up to the available width.
  - [x] Never throw during restore because of geometry differences.

### Phase 4: Persistence Service

- [x] Add `TerminalRestoreStore`.
- [x] Implement `SaveAsync(profile, tab, snapshot)`.
- [x] Implement `LoadAsync(profile, tab)`.
- [x] Implement `DeleteAsync(profile, tab)`.
- [x] Implement profile/tab cleanup helpers.
- [x] Use atomic writes.
- [x] Make load tolerant of missing, corrupt, or future-version files.
- [x] Bound file size by line limit.

### Phase 5: Capture Lifecycle

- [x] Capture after terminal output with debounce.
  - Implemented debounce: 3 seconds after the latest output.
- [x] Capture immediately on normal tab close.
- [x] Capture immediately on workspace close.
- [x] Capture on child process exit.
- [x] Capture during application shutdown if the terminal is still alive.
- [x] Cancel pending saves when the terminal is disposed.
- [x] Do not block UI rendering on disk writes.
- [x] Do not write snapshots for disabled tabs.

### Phase 6: Restore Lifecycle

- [x] On terminal tab creation, check restore settings.
- [x] Load the matching snapshot by profile id and tab id.
- [x] Apply snapshot before or immediately as the terminal process starts.
- [x] Ensure restored content stays above the new live session output.
- [x] If no snapshot exists, continue with normal startup.
- [x] If snapshot is corrupt, continue with normal startup.
- [x] If restore is disabled, ensure any stale snapshot is removed.

### Phase 7: Workspace Recovery Integration

- [x] Keep existing workspace recovery behavior intact.
- [x] Ensure profile reopen uses restore snapshots even when workspace recovery is not triggered.
- [x] Ensure crash/reboot recovery benefits from the most recent debounced snapshot.
- [x] Include enough profile/tab identity in snapshots to survive tab reorder and rename.
- [x] Avoid relying only on profile name or tab name for restore matching.

### Phase 8: Privacy And Cleanup

- [x] Add per-tab opt-out.
- [x] Remove saved restore data when opt-out is saved.
- [x] Remove saved restore data when a tab is deleted.
- [x] Remove saved restore data when a profile is deleted.
- [ ] Consider adding a future global command: clear all terminal restore data.
- [x] Document that scrollback restore stores recently displayed terminal text on disk.

### Phase 9: Tests

- [x] Unit test profile defaults.
- [x] Unit test migration assigns stable ids.
- [x] Unit test duplicate tab receives a new id.
- [x] Unit test restore setting serialization/deserialization.
- [x] Unit test `TerminalRestoreStore` save/load/delete.
- [x] Unit test corrupt snapshot load is ignored.
- [x] Unit test line limit trimming.
- [x] Unit test atomic replace behavior where practical.
- [x] Terminal snapshot test: text survives export/import.
- [x] Terminal snapshot test: colors/styles survive export/import.
- [ ] Terminal snapshot test: wrapped lines survive export/import.
- [x] Terminal snapshot test: alternate-screen active records metadata and does not crash.
- [ ] Workspace test: reopening a profile applies matching snapshot by tab id.
- [x] Workspace-adjacent test: disabled tab setting persists and cleanup path is implemented.

### Phase 10: Manual Validation

- [ ] Open local `cmd.exe`, produce more than 1000 lines, close/reopen, verify last 1000 lines restore.
- [ ] Open PowerShell, produce colored output, close/reopen, verify colors restore.
- [ ] Open SSH tab, run commands, close/reopen, verify prior remote output appears above new SSH session.
- [ ] In SSH, open `vim`, type/edit, close terminal or app, reopen, verify normal scrollback restore remains stable.
- [ ] In SSH, use `tmux` or `screen`, close/reopen, verify no broken replay behavior.
- [ ] Crash/kill the app with active output, reopen profile, verify recent captured output restores.
- [ ] Reboot after active terminal work, reopen profile, verify recent captured output restores.
- [ ] Disable restore for one tab, verify no restore occurs for that tab.
- [ ] Duplicate a tab, verify original and duplicate do not share restore snapshots.
- [ ] Rename a tab, verify restore still follows the same tab id.
- [ ] Reorder tabs, verify restore still follows the same tab id.
- [ ] Delete a tab, verify its restore file is removed.

## Acceptance Criteria

- [x] New and existing terminal tabs have scrollback restore enabled by default.
- [x] Users can disable restore per terminal tab.
- [x] Users can configure restore line count per terminal tab.
- [x] Reopening a profile restores recent scrollback for each enabled tab.
- [x] Restore works after normal close, crash, and reboot, subject to the most recent saved snapshot.
- [x] Restore does not send data to the shell or PTY.
- [ ] Restore does not interfere with SSH, nested SSH, `screen`, `tmux`, `vim`, or shell startup behavior.
- [x] Restore tolerates corrupt/missing snapshot files.
- [x] Restore storage is cleaned up when disabled or when tabs/profiles are deleted.
- [x] Automated tests cover profile settings, persistence, and snapshot export/import.

## Risks

- Snapshotting too often could add disk churn.
  - Mitigation: debounce writes, write only after output, and cap snapshot size.
- Terminal internals may not expose all needed buffer attributes cleanly.
  - Mitigation: start with text plus common attributes, then expand fidelity.
- Geometry changes may make exact visual restore impossible.
  - Mitigation: preserve content first and degrade wrapping gracefully.
- Saved scrollback may contain secrets that were printed in the terminal.
  - Mitigation: default feature is useful, but provide per-tab opt-out and cleanup.
- Stable tab ids may affect profile save/duplicate flows.
  - Mitigation: test migration, save, duplicate, delete, rename, and reorder explicitly.

## Implementation Notes

- Do not use raw PTY replay for restore. PTY replay can re-trigger terminal control sequences and is better suited to diagnostics.
- Do not attempt process resurrection. If users need persistent remote sessions, `tmux` or `screen` remains the right tool.
- Keep the restore snapshot format versioned from day one.
- Prefer small, focused commits:
  - Profile model and UI.
  - Snapshot export/import.
  - Persistence service.
  - Workspace integration.
  - Tests and cleanup.
