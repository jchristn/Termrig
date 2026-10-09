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
