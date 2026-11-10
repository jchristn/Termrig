# Installing Termrig on Linux

Termrig is not distributed as a signed download. You build it from source,
which takes a few minutes. The result is an app in your application menu that
you can pin to the dock. These steps were written for Ubuntu and should work on
most desktop Linux distributions.

## What you need

- A 64-bit Linux desktop (x64 or ARM64).
- The [.NET 10 SDK](https://learn.microsoft.com/dotnet/core/install/linux).
- Git. On Ubuntu:

  ```sh
  sudo apt install git
  ```

## Install

1. Open a terminal.
2. Download the source:

   ```sh
   git clone https://github.com/jchristn/Termrig
   cd Termrig
   ```

3. Build and install the app:

   ```sh
   ./packaging/linux/build-app.sh
   ```

   This installs Termrig for your user account and adds it to your
   application menu. You don't need `sudo`. It installs:

   - the app in `~/.local/share/termrig/`
   - the launcher in `~/.local/share/applications/com.jchristn.Termrig.desktop`
   - the icon in `~/.local/share/icons/hicolor/scalable/apps/`

4. Open your application menu (on Ubuntu, press the Super key), type
   **Termrig**, and press Enter.

## Pin to the dock

1. Open Termrig from the application menu.
2. Right-click the Termrig icon in the dock.
3. Choose **Pin to Dash** (Ubuntu) or **Add to Favorites** (GNOME).

## Update

1. Quit Termrig.
2. In the `Termrig` folder, run:

   ```sh
   git pull
   ./packaging/linux/build-app.sh
   ```

Your application menu entry and dock pin keep working.

## Uninstall

1. Quit Termrig.
2. Delete what step 3 installed:

   ```sh
   rm -rf ~/.local/share/termrig
   rm ~/.local/share/applications/com.jchristn.Termrig.desktop
   rm ~/.local/share/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg
   ```

Or run `./remove-tool.sh` from the `Termrig` folder, which does the same.

Your profiles and settings stay in `~/.termrig/`. Delete that folder too if
you want to remove them.

## Notes

- **Termrig doesn't appear in the menu yet:** log out and back in. Some
  desktops only rescan launchers at login.
- **Two Termrig icons in the dock:** unpin both, open Termrig from the
  application menu, then pin the running icon again.
- **Command-line launcher (optional):** `./install-tool.sh` installs a `trig`
  command that starts Termrig from any terminal, and also installs the app as
  in step 3. `./remove-tool.sh` removes both.

### Source scripts

`scripts/linux/` has the same scripts as every other `scripts/<os>/` folder.
Each takes no arguments (`-h` shows help) and refuses to run while Termrig is
running:

| Script | What it does |
| --- | --- |
| `install-tool.sh` | Builds and installs the `trig` command and the app (the three paths in step 3). Fails if either is already installed. |
| `reinstall-tool.sh` | Rebuilds both, then replaces the installed copies. Works when nothing is installed. |
| `remove-tool.sh` | Removes the `trig` command, the app, the launcher and the icon. Succeeds when nothing is installed. |
| `run.sh` | Rebuilds the app in `~/.local/share/termrig/` and opens that copy. |
| `kill-termrig.sh` | Stops every Termrig and `trig` process you are running. Tabs inside Termrig close with it. |

## WSL

Use `scripts/wsl2/` on WSL 2 and `scripts/wsl/` on WSL 1, not `scripts/linux/`.
They have the same scripts as `scripts/linux/` and install the same things
inside the distro, but use the Linux `dotnet` inside WSL (never the Windows
one), drop Windows entries from the PATH, add `~/.dotnet/tools`, and stop on
CRLF checkouts. Each refuses to run under the other WSL version. They only
install into the distro; use `scripts\windows\` for the Windows install.

- **WSL 2:** the Termrig window needs WSLg (Windows 11, or Windows 10 with the
  Store version of WSL). If `run.sh` says no display was found, run
  `wsl --update` and `wsl --shutdown` from Windows, then reopen the distro.
- **WSL 1:** there is no WSLg. Installing works, but opening Termrig needs an
  X server running on Windows (for example VcXsrv) and `DISPLAY` set; without
  it `run.sh` stops with a clear message. Otherwise use
  `scripts\windows\install-tool.bat` from Windows.
- Clone into the Linux file system (for example `~/src`), not `/mnt/c`. Builds
  there are much faster.
