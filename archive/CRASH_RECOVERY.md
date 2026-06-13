# Crash Recovery Implementation Plan

Status: Implemented; automated verification complete; manual QA pending
Last updated: 2026-06-13
Feature owner: Product Manager
Architecture owner: Principal Architect
Implementation owner: Engineering Manager
Quality owner: QA Engineer

## Goal

Termrig should recover gracefully after an application crash. When the app restarts after an unclean shutdown, it should ask whether to reopen the terminal profile workspaces that were open at the time of the crash. Termrig should also write an application crash log whenever it crashes, using this exact path and filename shape:

```text
~/.termrig/crashes/crash-yyyyMMdd-HHmmss.log
```

The recovery prompt restores profile workspaces, not terminal process state or shell scrollback. A restored workspace relaunches the saved profile using the same launch behavior Termrig already uses when a user opens a profile.

## Requirements Sources

This plan accounts for the applicable requirements under `C:\code\agents`.

| Source | Applicable guidance | Plan impact | Status |
| --- | --- | --- | --- |
| `REPOSITORY_REQUIREMENTS.md` | Keep source under `src/`; preserve root housekeeping files. | New implementation files should live under `src/Termrig.Core`, `src/Termrig.App`, and existing `src/Test.*` projects. | [ ] |
| `CODE_STYLE.md` | Namespace first; usings inside namespace; no `var`; no tuples; XML docs for public members; one class or enum per file; cancellable async APIs; meaningful exceptions; nullable enabled. | New models, services, dialogs, and tests must follow existing Termrig style and the stricter agent rules. | [ ] |
| `BACKEND_ARCHITECTURE.md` | Thin entry points, typed models, explicit composition, strong service boundaries, structured JSON contracts. | Put persistence in Core stores/models; keep `Program.cs` and `App.axaml.cs` orchestration thin; avoid ad hoc JSON or raw `JsonElement`. | [ ] |
| `BACKEND_TEST_ARCHITECTURE.md` | Shared Touchstone descriptors consumed by automated, xUnit, and NUnit runners. | Add exhaustive test descriptors to `src/Test.Shared`; verify through `src/Test.Automated`, `src/Test.Xunit`, and `src/Test.Nunit`. | [ ] |
| `I18N.md` | User-facing text should be localizable and not persisted as rendered text. | New prompt copy should be centralized in a localization-ready place; persisted recovery state must store semantic data, not English strings. | [ ] |
| `FRONTEND_ARCHITECTURE.md` | Dialogs and responsive UI must remain accessible, robust with long strings, and free of layout overlap. | The Avalonia confirmation dialog must support keyboard use, focus order, long profile counts/names, and screen-reader-accessible labels. | [ ] |
| `AUTHENTICATION.md` | Sensitive data must not leak into logs; logs need useful request/process context. | Crash logs must avoid terminal output, environment secrets, shell command contents beyond already-saved profile startup script policy, and raw process data that may contain secrets. | [ ] |
| `WRITING_DOCUMENTS.md` | Documents should be concrete, owned, and actionable. | This file uses checkboxes, owners, acceptance criteria, and evidence fields for progress annotation. | [ ] |
| `personas/README.md` | Major decisions have clear owners. | Architecture, implementation, quality, reliability, security, and documentation ownership are assigned below. | [ ] |

## Current Code Facts

- `src/Termrig.Core/Services/CrashLogStore.cs` already writes logs under `~/.termrig/crashes`, but its current filename format includes timestamp, profile, and tab names. It does not yet satisfy `crash-yyyyMMdd-HHmmss.log` for application crashes.
- `src/Termrig.App/App.axaml.cs` registers `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` in `Initialize()`.
- `src/Termrig.App/Program.cs` detaches into a child process unless `TERMRIG_FOREGROUND=1` or `--termrig-detached-child` is present. Crash tracking must run in the real Avalonia child process, not the short-lived parent launcher.
- `src/Termrig.App/Views/MainWindow.axaml.cs` tracks open workspaces in `_WorkspaceWindows`, opens them through `OpenWorkspace(TerminalProfile profile)`, and removes them when the workspace window closes.
- `MainWindow.LoadProfilesAsync()` loads profiles, refreshes the UI, then calls `OpenAutoOpenProfiles()`. Crash restore prompting should fit into this startup sequence before auto-open profiles are opened.
- `src/Termrig.App/Views/TerminalWorkspaceWindow.axaml.cs` logs terminal process failures separately. Those are terminal/session failures, not necessarily Termrig application crashes.
- `src/Test.Shared/TermrigSuites.cs` already contains Touchstone suites for crash logs, profile persistence, profile folders, color schemes, shell launch behavior, and XTerm regressions.

## Non-Goals

- Do not restore shell process memory, scrollback contents, command history, environment variables, or unsaved terminal output.
- Do not preserve transient unsaved tabs unless a later requirement explicitly asks for full runtime workspace persistence.
- Do not treat a terminal process exiting with a non-zero code as a Termrig application crash.
- Do not prompt after a clean app shutdown, even if terminal shells were closed as part of that shutdown.
- Do not add authentication, remote sync, cloud storage, or account concepts.

## Architecture

Crash recovery should use a durable runtime state file, not the crash handler, to know what was open. Crash handlers are best-effort and may not execute on hard process termination, native crashes, power loss, or abrupt OS shutdown. Runtime state should be updated when workspaces open and close; clean shutdown should clear or mark the state clean.

### Proposed Files

| File | Purpose | Owner | Status |
| --- | --- | --- | --- |
| `src/Termrig.Core/Models/WorkspaceRecoveryState.cs` | Typed JSON root model for the recovery file. | Software Engineer | [ ] |
| `src/Termrig.Core/Models/WorkspaceRecoveryWorkspace.cs` | One persisted open workspace instance. | Software Engineer | [ ] |
| `src/Termrig.Core/Services/WorkspaceRecoveryStore.cs` | Atomic JSON persistence, detection, normalization, and clean-shutdown marking. | Software Engineer | [ ] |
| `src/Termrig.App/Views/CrashRecoveryPromptWindow.axaml` | Confirmation dialog shown on restart after a crash. | UX Designer + Software Engineer | [ ] |
| `src/Termrig.App/Views/CrashRecoveryPromptWindow.axaml.cs` | Dialog behavior and result handling. | Software Engineer | [ ] |
| `src/Termrig.App/Services/CrashRecoveryStartupCoordinator.cs` | Optional app-layer orchestration if `MainWindow` becomes too broad. | Principal Architect | [ ] |
| `src/Test.Shared` additions | Touchstone coverage for stores, crash log naming, and recovery policy. | QA Engineer + Automation Engineer | [ ] |

Keep one class per file. Do not place nested model classes inside the store.

### Recovery State Path

Use a file under the existing application directory:

```text
~/.termrig/workspace-recovery.json
```

Add a constant such as `WorkspaceRecoveryFilename` to `Termrig.Core.Constants` if implementation follows the current store pattern.

### Recovery State Shape

Use a strongly typed JSON model. Store UTC timestamps and stable identifiers. Persist semantic data only; do not persist rendered prompt text.

```json
{
  "schemaVersion": 1,
  "runId": "a6d6c42c80e042d3af62cb9d4a39e95e",
  "processId": 12345,
  "startedUtc": "2026-06-13T18:22:00.0000000Z",
  "lastUpdatedUtc": "2026-06-13T18:25:00.0000000Z",
  "cleanShutdown": false,
  "restorePromptHandled": false,
  "openWorkspaces": [
    {
      "workspaceId": "2e8d6d1d3d2d4f8c8bbf2aaae15b9843",
      "profileId": "profile-id",
      "profileName": "Work",
      "openedUtc": "2026-06-13T18:23:00.0000000Z"
    }
  ]
}
```

`workspaceId` is required because a user may open the same profile multiple times. Restoring should reopen the same number of workspace instances when duplicates exist.

### Store API

`WorkspaceRecoveryStore` should mirror the existing store style while meeting the stricter agent requirements for cancellable async methods.

Checklist:

- [ ] Constructor accepts an optional directory path and defaults to `~/.termrig`.
- [ ] Public `DirectoryPath` and `FilePath` members expose resolved paths.
- [ ] `Task<WorkspaceRecoveryState?> LoadAsync(CancellationToken token = default)` loads and normalizes state.
- [ ] `Task SaveAsync(WorkspaceRecoveryState state, CancellationToken token = default)` writes atomically.
- [ ] `Task<WorkspaceRecoveryState> MarkRunStartedAsync(string runId, int processId, CancellationToken token = default)` sets `cleanShutdown = false`.
- [ ] `Task RegisterWorkspaceOpenedAsync(string runId, WorkspaceRecoveryWorkspace workspace, CancellationToken token = default)` adds a workspace instance.
- [ ] `Task RegisterWorkspaceClosedAsync(string runId, string workspaceId, CancellationToken token = default)` removes exactly one workspace instance.
- [ ] `Task MarkCleanShutdownAsync(string runId, CancellationToken token = default)` marks clean or deletes the file.
- [ ] `Task MarkRestorePromptHandledAsync(CancellationToken token = default)` prevents repeat prompts after the user declines or after restore is attempted.
- [ ] `Task<WorkspaceRecoveryState?> GetPendingCrashAsync(CancellationToken token = default)` returns state only when `cleanShutdown == false`, `restorePromptHandled == false`, and `openWorkspaces` has at least one item.
- [ ] Use `JsonSerializerOptions { WriteIndented = true }` and typed models.
- [ ] Use a temporary file plus atomic replace or move so a process crash cannot leave a half-written JSON file.
- [ ] Validate arguments with guard clauses and meaningful exception messages.
- [ ] Use `ConfigureAwait(false)` in Core code.
- [ ] Check cancellation before filesystem operations and after reads where useful.

### Crash Log API

`CrashLogStore` should distinguish application crashes from terminal process failures.

Checklist:

- [ ] Add or modify an application-crash method that writes exactly `crash-yyyyMMdd-HHmmss.log`.
- [ ] Continue writing under `~/.termrig/crashes`.
- [ ] Include timestamp, Termrig version, OS/runtime information, process id, thread id if available, exception type, message, stack trace, and recovery state summary.
- [ ] Guard against duplicate crash logs when both `UnhandledException` and the top-level `Program.Main` catch path observe the same failure.
- [ ] Do not include raw terminal output, clipboard contents, full environment variables, credentials, tokens, or other high-risk data.
- [ ] Decide and document what happens if two crash logs would use the same second-level filename. Preferred behavior: one application crash log per process via an interlocked guard.
- [ ] Keep terminal process failure logging separate. If terminal logs retain profile/tab names in filenames, document that they are not application crash logs.

### Startup Flow

The startup flow should become deterministic and easy to test.

Checklist:

- [ ] `Program.Main` should wrap the actual Avalonia lifetime call in a top-level `try/catch` and write an application crash log before rethrowing or exiting.
- [ ] Do not start recovery tracking in the parent process that only launches the detached child.
- [ ] `App.Initialize()` should continue registering crash handlers early.
- [ ] Application crash handlers should write the crash log and leave recovery state dirty. They should not mark a clean shutdown.
- [ ] Track an app-level `_CrashDetected` flag so normal `desktop.Exit` handling does not mark clean after an unhandled crash path.
- [ ] Create the current run id once in the Avalonia child process.
- [ ] Mark the run started before workspaces can open.
- [ ] After profiles load, call recovery detection before `OpenAutoOpenProfiles()`.
- [ ] If pending crash state exists, show the restore prompt.
- [ ] If the user chooses restore, reopen the recorded workspaces and skip normal auto-open for that launch.
- [ ] If the user declines restore, mark the pending state handled and then run normal auto-open.
- [ ] If no pending crash state exists, run normal auto-open as today.
- [ ] Apply explicit startup commands after the restore decision. A `tr open <profile>` command should still open the requested profile even if crash restore is accepted.
- [ ] Avoid duplicate auto-open windows when a restored profile is also marked `AutoOpen`.

### Workspace Lifecycle Flow

`MainWindow` already owns workspace open and close tracking in memory. Persist recovery state at the same boundaries.

Checklist:

- [ ] When `OpenWorkspace(TerminalProfile profile)` creates a `TerminalWorkspaceWindow`, assign a new workspace instance id.
- [ ] Register the workspace with `WorkspaceRecoveryStore` after the window is created and before or immediately after it is shown.
- [ ] Persist `profile.Id`, `profile.Name`, and the generated workspace id.
- [ ] Expose workspace id and profile id from `TerminalWorkspaceWindow` if needed for clean close tracking.
- [ ] On workspace `Closed`, remove exactly that workspace id from recovery state.
- [ ] If a user opens the same profile five times, persist five workspace entries and restore five windows.
- [ ] If a workspace closes cleanly before a crash, it must not be restored.
- [ ] On clean application exit, mark clean shutdown after command server disposal and workspace close handling complete.

### Restore Matching Rules

Use profile id first. Use profile name only as a fallback for legacy or manually edited state.

Checklist:

- [ ] If `profileId` matches a loaded profile, open that profile.
- [ ] If `profileId` is missing or not found and exactly one profile name matches case-insensitively, open the name match.
- [ ] If no loaded profile matches, skip that workspace and include it in the prompt details or a post-restore summary.
- [ ] If multiple profiles share the fallback name, skip the ambiguous workspace.
- [ ] Preserve duplicate workspace entries.
- [ ] Never create a new profile from recovery state.
- [ ] Mark restore handled even when some profiles are skipped, so the prompt does not repeat forever.

### Prompt UX

The prompt should be short and operational.

Suggested text, centralized for future localization:

- Title: `Restore previous workspaces?`
- Message: `Termrig did not shut down cleanly. Reopen {count} workspaces from the previous session?`
- Primary button: `Restore`
- Secondary button: `Don't Restore`

Checklist:

- [ ] Use plural-aware message generation, not string concatenation that assumes English grammar.
- [ ] Put new strings in a single localization-ready location rather than scattering literals through code-behind.
- [ ] Do not persist the rendered prompt text in JSON.
- [ ] Dialog opens after `MainWindow` is available so it has an owner.
- [ ] `Enter` activates Restore only if that matches existing dialog conventions.
- [ ] `Escape` and window close behave like `Don't Restore`.
- [ ] Long profile names or a high workspace count do not overflow the dialog.
- [ ] The dialog is keyboard reachable and has a clear focus order.
- [ ] The prompt appears once per pending crash state.

## Implementation Workstreams

### 1. Core Persistence

Owner: Software Engineer
Reviewer: Principal Architect

- [ ] Add `WorkspaceRecoveryState`.
- [ ] Add `WorkspaceRecoveryWorkspace`.
- [ ] Add `WorkspaceRecoveryStore`.
- [ ] Add or update constants for `workspace-recovery.json`.
- [ ] Implement atomic JSON writes.
- [ ] Normalize loaded null collections and string fields.
- [ ] Add cancellation and guard clause behavior.
- [ ] Add tests before app wiring where practical.

Completion evidence:

- [ ] Store tests pass in `Test.Automated`.
- [ ] Store tests pass in xUnit and NUnit wrappers.
- [ ] Manual inspection confirms one class per file and in-namespace usings.

### 2. Crash Logging

Owner: Software Engineer
Reviewer: Security Engineer + Site Reliability Engineer

- [ ] Add exact application crash filename support.
- [ ] Include useful diagnostic context in the log body.
- [ ] Add duplicate-log guard for one app crash per process.
- [ ] Avoid logging secrets or raw terminal data.
- [ ] Ensure crash log directory creation remains automatic.
- [ ] Keep terminal process failure logs conceptually separate from application crash logs.

Completion evidence:

- [ ] New tests prove exact `crash-yyyyMMdd-HHmmss.log` naming.
- [ ] Crash log body includes exception details.
- [ ] Security review confirms no obvious sensitive data capture.

### 3. App Startup Orchestration

Owner: Principal Architect + Software Engineer
Reviewer: Engineering Manager

- [ ] Decide whether orchestration stays in `MainWindow` or moves to a small coordinator service.
- [ ] Mark run started only in the Avalonia child process.
- [ ] Add top-level crash logging around `StartWithClassicDesktopLifetime`.
- [ ] Ensure crash handlers do not mark clean shutdown.
- [ ] Reorder `LoadProfilesAsync()` flow so crash restore decision occurs before auto-open.
- [ ] Preserve explicit startup command behavior.
- [ ] Avoid duplicate windows from auto-open plus restore.

Completion evidence:

- [ ] Startup sequence is covered by service-level tests or clear integration tests.
- [ ] Manual crash/restart checks pass.

### 4. Workspace Lifecycle Wiring

Owner: Software Engineer
Reviewer: QA Engineer

- [ ] Persist workspace open in `OpenWorkspace`.
- [ ] Persist workspace close in the existing `Closed` handler.
- [ ] Track duplicate profile workspace instances separately.
- [ ] Ensure clean workspace closure removes state before app shutdown.
- [ ] Ensure crashes leave the last persisted open workspace list intact.

Completion evidence:

- [ ] Tests prove duplicate instances are preserved and individually removed.
- [ ] Manual test with five open workspaces restores five windows.

### 5. Restore Prompt UI

Owner: UX Designer + Software Engineer
Reviewer: Product Manager + QA Engineer

- [ ] Add the prompt window.
- [ ] Centralize localizable strings.
- [ ] Wire prompt result into startup flow.
- [ ] Add handling for skipped missing profiles.
- [ ] Verify keyboard and close behaviors.
- [ ] Verify no layout overflow with long copy.

Completion evidence:

- [ ] Manual UI validation at normal, narrow, and large desktop window sizes.
- [ ] Prompt screenshot or QA note attached to the implementing PR.

### 6. Documentation

Owner: Documentation Engineer
Reviewer: Product Manager

- [ ] Update `README.md` only if the feature should be user-visible in documentation.
- [ ] Update `CHANGELOG.md` with crash recovery and crash log behavior.
- [ ] Document crash log path and recovery limitations.
- [ ] Add troubleshooting notes for skipped/deleted profiles.

Completion evidence:

- [ ] Docs mention exact crash log path.
- [ ] Docs state that shells and scrollback are relaunched, not resumed.

## Test Plan

All automated tests should be added to `src/Test.Shared` first and consumed by the existing automated, xUnit, and NUnit runners. Shared tests must not write to the console. Use temporary directories for filesystem tests and clean them up in `finally` blocks.

### Test Suites To Add Or Expand

| Suite | Location | Owner | Status |
| --- | --- | --- | --- |
| `WorkspaceRecoveryStoreSuite` | `src/Test.Shared/TermrigSuites.cs` or split suite file if needed | Automation Engineer | [ ] |
| `CrashLogStoreSuite` expansion | Existing `CrashLogStoreSuite` | Automation Engineer | [ ] |
| `CrashRecoveryPolicySuite` | Shared tests for matching and startup policy, preferably against a small app-layer service | QA Engineer | [ ] |
| `CrashRecoveryManualChecklist` | PR checklist or release QA notes | QA Engineer | [ ] |

### WorkspaceRecoveryStore Coverage

- [ ] Constructor resolves default directory to `~/.termrig`.
- [ ] Constructor accepts a custom temp directory.
- [ ] Constructor rejects invalid custom directory input only when the existing store conventions require it.
- [ ] `FilePath` ends with `workspace-recovery.json`.
- [ ] `MarkRunStartedAsync` creates the directory and file.
- [ ] New run state has `schemaVersion = 1`.
- [ ] New run state has `cleanShutdown = false`.
- [ ] New run state has `restorePromptHandled = false`.
- [ ] New run state stores run id and process id.
- [ ] New run state initializes an empty `openWorkspaces` list.
- [ ] `RegisterWorkspaceOpenedAsync` stores profile id, profile name, workspace id, and opened timestamp.
- [ ] Opening two different profiles stores two entries.
- [ ] Opening the same profile twice stores two entries with distinct workspace ids.
- [ ] `RegisterWorkspaceClosedAsync` removes only the matching workspace id.
- [ ] Closing one duplicate profile workspace leaves the other duplicate entry.
- [ ] Closing an unknown workspace id is harmless or returns a documented result.
- [ ] `MarkCleanShutdownAsync` prevents future pending crash detection.
- [ ] Clean shutdown either deletes the file or marks `cleanShutdown = true`; test whichever behavior is chosen.
- [ ] `MarkRestorePromptHandledAsync` prevents repeat prompting.
- [ ] Pending crash is detected when shutdown is unclean, prompt not handled, and open workspace count is greater than zero.
- [ ] Pending crash is not detected when the file is missing.
- [ ] Pending crash is not detected when shutdown is clean.
- [ ] Pending crash is not detected when prompt was already handled.
- [ ] Pending crash is not detected when open workspace count is zero.
- [ ] Loading normalizes a null `openWorkspaces` value to an empty list.
- [ ] Loading normalizes null string fields to empty strings or skips invalid entries, matching the implementation policy.
- [ ] Malformed JSON does not crash startup. Preferred behavior: write a crash or diagnostic log, ignore recovery, and preserve the bad file with a `.bad` suffix for support.
- [ ] Unsupported schema version is ignored safely or migrated by documented rules.
- [ ] Cancellation before load throws `OperationCanceledException`.
- [ ] Cancellation before save throws `OperationCanceledException`.
- [ ] Invalid state passed to save throws `ArgumentNullException`.
- [ ] Invalid required workspace fields throw `ArgumentNullException` or `ArgumentException`.
- [ ] Atomic write leaves either the old valid file or new valid file, never an intentionally partial file in normal operation.
- [ ] Timestamps are UTC.

### CrashLogStore Coverage

- [ ] Default directory resolves to `~/.termrig/crashes`.
- [ ] Custom crash directory is created.
- [ ] Application crash log filename matches `^crash-[0-9]{8}-[0-9]{6}\.log$`.
- [ ] Application crash log is written under the crash directory.
- [ ] Application crash log body contains ISO timestamp.
- [ ] Application crash log body contains exception type.
- [ ] Application crash log body contains exception message.
- [ ] Application crash log body contains stack trace when available.
- [ ] Application crash log body contains process id.
- [ ] Application crash log body contains recovery workspace count when supplied.
- [ ] Application crash log does not include raw terminal buffer text.
- [ ] Application crash log method validates summary/details or exception arguments.
- [ ] Async application crash log write honors cancellation.
- [ ] Existing terminal failure log tests are updated if filename behavior changes.
- [ ] Terminal failure log behavior remains covered separately from application crash logs.
- [ ] Duplicate crash handler invocations in one process do not create duplicate application crash logs if a guard is implemented.

### Restore Matching And Policy Coverage

Prefer testing this through a pure coordinator or policy class so tests do not need to instantiate Avalonia windows.

- [ ] Matching uses `profileId` before `profileName`.
- [ ] Matching by id succeeds after profile rename.
- [ ] Name fallback succeeds only when exactly one profile matches case-insensitively.
- [ ] Name fallback skips ambiguous duplicate names.
- [ ] Missing profile is skipped.
- [ ] Duplicate workspace entries produce duplicate restore actions.
- [ ] Restore accepted skips auto-open profiles for that launch.
- [ ] Restore declined runs normal auto-open profiles.
- [ ] No pending crash runs normal auto-open profiles.
- [ ] Explicit startup command profile opens after restore accepted.
- [ ] Explicit startup command profile opens after restore declined.
- [ ] Explicit startup command profile opens when no pending crash exists.
- [ ] Startup command is not lost if restore prompt is shown.
- [ ] Prompt handled is saved after accept.
- [ ] Prompt handled is saved after decline.
- [ ] Prompt handled is saved even when every recovered profile is missing.
- [ ] Recovery state from a different or stale run is still eligible if it is the latest unclean state and contains open workspaces.

### App Integration Coverage

Automated GUI coverage may be limited. If adding UI automation is too expensive, cover the logic through stores/coordinators and require the manual checks below before merge.

- [ ] Starting Termrig normally creates or updates dirty recovery state in the child process.
- [ ] Closing Termrig normally marks recovery state clean.
- [ ] Opening one profile writes one workspace entry.
- [ ] Opening five profile workspaces writes five workspace entries.
- [ ] Closing one of five workspaces removes one entry.
- [ ] Killing the process leaves dirty state with remaining workspaces.
- [ ] Restarting after process kill shows the restore prompt.
- [ ] Accepting restore reopens the recorded profile workspaces.
- [ ] Declining restore does not reopen recorded workspaces and does not prompt again on the next clean restart.
- [ ] Auto-open profiles still open on normal startup.
- [ ] Auto-open profiles do not duplicate restored profiles when restore is accepted.
- [ ] `tr open <profile>` still works when an existing instance is running.
- [ ] `tr open <profile>` still works when it starts a new instance and pending crash state exists.
- [ ] Crash log is written for an unhandled managed exception.
- [ ] Crash log is written for a top-level startup exception.
- [ ] Unobserved task exceptions remain diagnostic and do not incorrectly trigger restore behavior unless they terminate the app.

### Manual QA Matrix

Run these checks on Windows first. Repeat the filesystem/path and clean-shutdown checks on macOS and Linux before release if Termrig supports those packages.

| Scenario | Steps | Expected result | Status | Notes |
| --- | --- | --- | --- | --- |
| Clean launch and exit | Launch app, open no workspace, close app. | No restore prompt on next launch. | [ ] | |
| One workspace crash | Open one profile workspace, kill Termrig process, relaunch. | Prompt appears; Restore opens one workspace. | [ ] | |
| Five workspace crash | Open five profile workspaces, kill Termrig process, relaunch. | Prompt says five workspaces; Restore opens five windows. | [ ] | |
| Duplicate profile instances | Open same profile three times, kill, relaunch, restore. | Three workspace windows open. | [ ] | |
| Decline restore | Crash with open workspaces, relaunch, choose Don't Restore, relaunch again. | No old workspaces open; no repeat prompt. | [ ] | |
| Auto-open conflict | Mark profile AutoOpen, crash with same profile open, restore. | Restored workspace opens once per recorded entry; no extra auto-open duplicate. | [ ] | |
| Command-line open conflict | Crash with pending state, run `tr open SomeProfile`. | Restore decision is offered; explicit profile open still happens after decision. | [ ] | |
| Deleted profile | Crash with profile open, delete profile from JSON or another instance before relaunch. | Missing profile is skipped without blocking app startup. | [ ] | |
| Corrupt recovery file | Replace recovery JSON with invalid JSON, relaunch. | App starts; no crash loop; invalid file is handled by documented policy. | [ ] | |
| Crash log filename | Trigger managed crash. | File exists as `~/.termrig/crashes/crash-yyyyMMdd-HHmmss.log`. | [ ] | |
| Prompt keyboard | Trigger prompt, use Tab, Enter, Escape. | Focus order is clear; Escape declines; selected action behaves predictably. | [ ] | |
| Long labels | Use long profile names and trigger prompt. | Dialog text does not overlap or clip action buttons. | [ ] | |

### Verification Commands

Run from the repository root after implementation:

```powershell
dotnet build src\Termrig.slnx
dotnet run --project src\Test.Automated -- --results artifacts\crash-recovery-tests.json
dotnet test src\Test.Xunit
dotnet test src\Test.Nunit
```

If any command cannot run in the development environment, record the reason in the PR and attach the strongest available substitute evidence.

## Acceptance Criteria

The feature is complete only when all criteria below are met.

- [ ] After a Termrig application crash with open workspace windows, restart shows a prompt asking whether to reopen the previously open profile workspaces.
- [ ] Accepting the prompt reopens the recorded profile workspaces.
- [ ] Declining the prompt does not reopen them and does not prompt again for the same crash state.
- [ ] Clean shutdown never triggers the restore prompt.
- [ ] The recovery state is persisted before a crash handler is needed.
- [ ] The restore logic uses profile id first and profile name only as a safe fallback.
- [ ] Duplicate open instances of the same profile are restored as duplicate workspace windows.
- [ ] Auto-open profile behavior remains intact on normal startup.
- [ ] Auto-open profile behavior does not duplicate restored workspaces when restore is accepted.
- [ ] Explicit command-line open behavior remains intact.
- [ ] Application crashes write `~/.termrig/crashes/crash-yyyyMMdd-HHmmss.log`.
- [ ] Crash logs contain actionable exception diagnostics.
- [ ] Crash logs do not intentionally capture secrets, terminal buffer contents, clipboard contents, or full environment variables.
- [ ] Store, crash log, and restore policy tests are present in shared Touchstone suites.
- [ ] Automated, xUnit, and NUnit runners pass.
- [ ] Manual QA matrix is completed or consciously deferred with owner approval.
- [ ] New public members have XML documentation.
- [ ] New async Core methods accept `CancellationToken` and use `ConfigureAwait(false)`.
- [ ] New files follow one-class-per-file and no-tuple rules.
- [ ] User-facing strings are centralized for future localization.

## Risks And Mitigations

| Risk | Impact | Mitigation | Owner | Status |
| --- | --- | --- | --- | --- |
| Crash handler does not run | No crash log for hard kills or native failures. | Use continuous recovery-state persistence for restore; document crash logs as best-effort for unrecoverable process termination. | SRE | [ ] |
| Recovery state corrupts during write | App could crash or skip restore on restart. | Atomic write through temp file and safe malformed JSON handling. | Software Engineer | [ ] |
| Prompt repeats forever | Bad user experience after decline or partial restore. | Persist `restorePromptHandled` after accept, decline, or exhausted restore attempt. | QA Engineer | [ ] |
| Auto-open duplicates restored workspaces | Too many terminal windows open. | Explicit startup policy: accepted restore skips normal auto-open for that launch. | Product Manager | [ ] |
| Same profile opened multiple times is collapsed | User loses workspace count. | Persist runtime workspace instance ids and restore each entry. | Principal Architect | [ ] |
| Crash logs leak sensitive data | Security risk. | Log exception and app context only; do not log terminal output, environment, or clipboard. | Security Engineer | [ ] |
| Detached parent process pollutes recovery state | False restore prompts. | Start recovery tracking only in the Avalonia child process after detach decision. | Software Engineer | [ ] |
| UI strings become hard to localize later | I18N debt. | Centralize prompt text and use semantic persisted state. | UX Designer | [ ] |

## PR Review Checklist

Reviewers should not approve until each item has either evidence or an explicit accepted deferral.

- [ ] Product Manager confirms prompt behavior and auto-open conflict behavior.
- [ ] Principal Architect confirms state model, startup flow, and service boundaries.
- [ ] Engineering Manager confirms implementation scope and code style compliance.
- [ ] Security Engineer confirms crash log contents do not expose sensitive data.
- [ ] SRE confirms crash recovery semantics and diagnostic value.
- [ ] QA Engineer confirms automated and manual coverage.
- [ ] Documentation Engineer confirms README/CHANGELOG updates if applicable.

## Progress Notes

Use this section during implementation to annotate decisions, deviations, and completion evidence.

| Date | Owner | Note | Follow-up |
| --- | --- | --- | --- |
| 2026-06-13 | Codex | Initial actionable plan created. | Implement feature in a later code change. |
| 2026-06-13 | Codex | Implemented Core recovery persistence, restore planning, app startup/workspace wiring, exact application crash logs, restore prompt UI, and shared automated tests. `dotnet build`, automated Touchstone, xUnit, and NUnit passed for `net8.0` and `net10.0`. | Complete manual crash/restart QA matrix before release. |
