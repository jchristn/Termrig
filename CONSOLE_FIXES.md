# Console Rendering Fixes

## Problem

Console applications that redraw existing lines, especially `docker compose up -d` and `docker compose down`, render incorrectly in Termrig. Observed symptoms include duplicated progress rows, stale row fragments, cursor movement into prior output, and the shell prompt appearing inside a Docker progress row.

## Fixes Tried

| Attempt | Change | Result |
| --- | --- | --- |
| 1 | Cleared cached rendered line runs on a periodic timer for the selected terminal. | Failed. The display still showed duplicated and stale Docker Compose progress rows. This indicated the issue was not only stale Avalonia render caches. |
| 2 | Invalidated the inner `TerminalView` after clearing line caches. | Failed. Repaint frequency improved, but cursor-positioned redraw output still left stale text and misplaced prompts. |
| 3 | Persisted `Ctrl+plus`, `Ctrl+minus`, and reset font changes to the tab profile. | Succeeded for font persistence. Not directly related to Docker Compose rendering. |
| 4 | Set explicit XTerm options per tab: `ConvertEol = true`, configured scrollback, and `TermName = xterm-256color`. | Partially helped in a direct emulator repro by reducing horizontal cursor drift from bare line feeds, but failed in the real Docker Compose case. |
| 5 | Vendored `Iciclecreek.Avalonia.Terminal` into `src/ThirdParty` and changed Termrig to use the local project instead of the NuGet package. | Infrastructure succeeded. This enabled patching the PTY read path directly. |
| 6 | Patched the vendored `TerminalView` PTY read path to inject `ESC[2K` after bare carriage returns so carriage-return redraws clear the current line before repainting. | Failed. Docker Compose output still left duplicated rows and prompt text embedded in progress rows. |
| 7 | Added `COMPOSE_PROGRESS=plain` to the environment passed into spawned PTY processes. | Failed. Docker Compose switched to plain output, but colors and extended symbols were lost and the behavior still did not meet the requirement. Removed this mitigation. |
| 8 | Clamp the PTY width to one fewer column than the rendered terminal width. | Build, tests, and pack succeeded. Runtime validation is pending because `tr.exe` is still running and locking the installed global tool. Docker Compose pads progress rows to terminal width; this tests whether exact-width lines are wrapping before cursor-up redraws. |

## Direct Repro Findings

| Repro | Result |
| --- | --- |
| Synthetic XTerm.NET test with cursor-up and erase-line sequences in an 80x12 terminal. | Basic redraw worked when lines ended cleanly. |
| Synthetic XTerm.NET test with scrollback and no final newline before prompt. | Failed. The prompt overwrote the start of the previous progress row and left stale text after the prompt. |
| Synthetic XTerm.NET test with `ConvertEol = false`. | Failed badly. Bare line feeds preserved the current column and caused large horizontal offsets. |
| Synthetic XTerm.NET test with `ConvertEol = true`. | Improved horizontal offsets, but stale text remained when the rewritten progress block did not finish with a newline. |
| Synthetic XTerm.NET test with scrollback disabled. | Did not fix stale text. |

## Other Potential Fixes To Try

| Candidate | Status | Success/Failure | Notes |
| --- | --- | --- | --- |
| Patch XTerm.NET erase-line / cursor movement behavior directly and add regression tests upstream or locally. | Not tried |  | Most correct long-term route if emulator semantics are wrong. |
| Replace `Iciclecreek.Avalonia.Terminal` / `XTerm.NET` with another Avalonia terminal emulator. | Not tried |  | Higher risk and larger integration cost. |
| Capture raw PTY output from Docker Compose and replay it into XTerm.NET tests. | Not tried |  | Needed for a precise emulator regression test. |
| Add an option per tab to force plain progress environments (`COMPOSE_PROGRESS=plain`, `BUILDKIT_PROGRESS=plain`, etc.). | Tried globally | Failed | Plain mode loses colors and extended symbols, and the user wants dynamic redraws to work correctly. |
| Force a narrower PTY width or synchronize PTY resize after first layout before process launch. | Not tried |  | Could help if Docker formats rows for a wider PTY than the visible terminal. |
| Disable scrollback while a cursor-addressed progress block is active. | Not tried |  | Risky; could break normal terminal history. |
| Detect Docker Compose commands and route them through `--progress plain` or equivalent command rewriting. | Not tried |  | Brittle and command-specific. |
| Add a terminal setting to run shells with `TERM=dumb`. | Not tried |  | Broad workaround; likely degrades many CLI apps. |
