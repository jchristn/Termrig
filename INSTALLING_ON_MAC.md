# Installing Termrig on macOS

Termrig is not distributed as a signed download. You build it from source on
your Mac, which takes a few minutes. The result is `Termrig.app`, which you can
open from Finder, Spotlight, or the Dock.

## What you need

- macOS 12 or later (Apple silicon or Intel).
- The [.NET 10 SDK](https://dotnet.microsoft.com/download).
- Git and the Xcode command line tools. If you don't have them, run:

  ```sh
  xcode-select --install
  ```

## Install

1. Open Terminal.
2. Download the source:

   ```sh
   git clone https://github.com/jchristn/Termrig
   cd Termrig
   ```

3. Build and install the app:

   ```sh
   ./packaging/macos/build-app.sh
   ```

   This puts `Termrig.app` in `/Applications` (or `~/Applications` if
   `/Applications` isn't writable) and removes any copy left in the other
   location, so Spotlight (Cmd-Space → "termrig") always opens this build.

   To install somewhere else, pass the folder:

   ```sh
   ./packaging/macos/build-app.sh ~/Applications
   ```

4. Open Termrig:

   ```sh
   open /Applications/Termrig.app
   ```

   Or press Cmd-Space and type `termrig`.

## Keep Termrig in the Dock

1. Open Termrig (step 4 above).
2. Right-click the Termrig icon in the Dock.
3. Choose **Options → Keep in Dock**.

Pin `Termrig.app`, not a Termrig started from Terminal (`trig`, `go.sh`, or
`dotnet run`). Those show the generic "exec" icon in the Dock after Termrig
quits. If you have one of those pinned, right-click it, choose **Options**,
and uncheck **Keep in Dock**.

## Update

1. Quit Termrig.
2. In the `Termrig` folder, run:

   ```sh
   git pull
   ./packaging/macos/build-app.sh
   ```

Your pinned Dock icon keeps working. If Termrig was still running, Spotlight
switches to the old process instead of launching the new build, so quit first.

## Uninstall

1. Quit Termrig.
2. Delete `Termrig.app` from `/Applications` (or `~/Applications`), or run
   `./remove-tool.sh`.

Your profiles and settings stay in `~/.termrig/`. Delete that folder too if
you want to remove them.

## Notes

- **Security prompts:** an app you build on your own Mac opens without a
  Gatekeeper warning. If you copy it to another Mac, that Mac may block it.
  Right-click the app, choose **Open**, then **Open** again.
- **Old icon after an update:** run `killall Dock`.
- **Command-line launcher (optional):** `./install-tool.sh` installs a `trig`
  command that starts Termrig from any terminal, and also rebuilds
  `Termrig.app`. Remove both with `./remove-tool.sh`.

### Source scripts

`scripts/macos/` has the same scripts as every other `scripts/<os>/` folder.
Each takes no arguments (`-h` shows help) and refuses to run while Termrig is
running. The app goes in `/Applications/Termrig.app` (or `~/Applications` when
`/Applications` isn't writable); a copy in the other location is removed, and
signing must succeed before the installed app is replaced.

| Script | What it does |
| --- | --- |
| `install-tool.sh` | Builds and installs the `trig` command and `Termrig.app`. Fails if either is already installed. |
| `reinstall-tool.sh` | Rebuilds both, then replaces the installed copies. Works when nothing is installed. |
| `remove-tool.sh` | Removes the `trig` command and `Termrig.app` from both locations. Succeeds when nothing is installed. |
| `run.sh` | Rebuilds `Termrig.app` and opens it. `./go.sh` runs this on macOS. |
| `kill-termrig.sh` | Stops every Termrig and `trig` process you are running. Tabs inside Termrig close with it. |
