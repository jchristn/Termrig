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

- The desktop libraries Termrig's window uses. The install scripts install
  them for you (see [Desktop libraries](#desktop-libraries)).

## Desktop libraries

Termrig draws its window with Avalonia, which loads the X11 client libraries
and fontconfig when Termrig starts. Desktop installs already have them. WSL
distros and minimal or server images usually don't.

**The scripts install them for you.** `install-tool.sh`, `reinstall-tool.sh`
and `run.sh` (in `scripts/linux/`, `scripts/wsl/` and `scripts/wsl2/`), and
`packaging/linux/build-app.sh` (which the repository-root `install-tool.sh`
and `reinstall-tool.sh` use), check for each library before building. If any
are missing, they install them with `apt-get` (or `dnf` on Fedora) through
`sudo`, which may ask for your password. Set `TERMRIG_NO_DEPS=1` to skip that
and only list what's missing.

If the scripts can't install them (no `apt-get` or `dnf`, no `sudo`, or the
install fails), `install-tool.sh` and `reinstall-tool.sh` still install
Termrig and print the command to run yourself. `run.sh` stops before building.

Without these libraries, Termrig stops as soon as you start it, with an error
like this:

```text
Unhandled exception. System.DllNotFoundException: Unable to load shared library 'libICE.so.6' or one of its dependencies.
```

The library named can also be `libSM.so.6`, `libX11.so.6`, `libSkiaSharp` /
`libfontconfig.so.1`, or another one from the list below. Run any of the
install scripts, or install them all at once yourself.

Debian and Ubuntu, including WSL:

```sh
sudo apt install libx11-6 libice6 libsm6 libxext6 libxrandr2 libxi6 libxcursor1 libxfixes3 libfontconfig1
```

Fedora:

```sh
sudo dnf install libX11 libICE libSM libXext libXrandr libXi libXcursor libXfixes fontconfig
```

If you install them by hand, you don't need to rebuild or reinstall
afterwards. Start Termrig again. If Termrig itself hits a missing library, it
prints the same command instead of a stack trace.

`libgl1` (Debian/Ubuntu) or `mesa-libGL` (Fedora) is optional. Without it
Termrig draws in software, which still works.

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

- **`Unable to load shared library 'libICE.so.6'` (or another `lib*.so`):**
  run `./scripts/linux/reinstall-tool.sh` (or `scripts/wsl2/` on WSL 2),
  which installs the [desktop libraries](#desktop-libraries), or install them
  yourself.
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

- **Desktop libraries:** WSL distros (Ubuntu from the Microsoft Store, for
  example) don't include the X11 libraries Termrig needs. WSLg provides only
  the display server. The WSL scripts install the libraries inside the distro
  with `sudo apt-get`, which asks for your Linux password. Without them,
  `trig` fails with `Unable to load shared library 'libICE.so.6'`. See
  [Desktop libraries](#desktop-libraries).
- **WSL 2:** the Termrig window needs WSLg (Windows 11, or Windows 10 with the
  Store version of WSL). If `run.sh` says no display was found, run
  `wsl --update` and `wsl --shutdown` from Windows, then reopen the distro.
- **WSL 1:** there is no WSLg. Installing works, but opening Termrig needs an
  X server running on Windows (for example VcXsrv) and `DISPLAY` set; without
  it `run.sh` stops with a clear message. Otherwise use
  `scripts\windows\install-tool.bat` from Windows.
- Clone into the Linux file system (for example `~/src`), not `/mnt/c`. Builds
  there are much faster.
