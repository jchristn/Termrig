# Installing Termrig on Windows

Termrig is not distributed as a signed download. You build it from source on
your PC, which takes a few minutes. The result is a normal Windows app with a
Start menu shortcut that you can pin to the taskbar.

## What you need

- Windows 10 or 11 (x64 or ARM64).
- The [.NET 10 SDK](https://dotnet.microsoft.com/download).
- [Git for Windows](https://git-scm.com/download/win).

## Install

1. Open **PowerShell** (or Command Prompt).
2. Download the source:

   ```powershell
   git clone https://github.com/jchristn/Termrig
   cd Termrig
   ```

3. Build and install the app:

   ```powershell
   powershell -ExecutionPolicy Bypass -File packaging\windows\build-app.ps1
   ```

   To also put a shortcut on your desktop, add `-DesktopShortcut` to the end.

   This installs Termrig for your user account in
   `%LOCALAPPDATA%\Programs\Termrig` and adds **Termrig** to the Start menu.
   You don't need administrator rights.

4. Open the **Start** menu, type **Termrig**, and press Enter.

## Pin to the taskbar

1. Open the **Start** menu and type **Termrig**.
2. Right-click **Termrig** and choose **Pin to taskbar**.

You can also right-click the Termrig icon on the taskbar while it's running
and choose **Pin to taskbar**.

## Update

1. Quit Termrig.
2. In the `Termrig` folder, run:

   ```powershell
   git pull
   powershell -ExecutionPolicy Bypass -File packaging\windows\build-app.ps1
   ```

Your Start menu entry and taskbar pin keep working.

## Uninstall

1. Quit Termrig.
2. Delete the folder `%LOCALAPPDATA%\Programs\Termrig`.
3. Delete the shortcut `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Termrig.lnk`,
   and `Termrig.lnk` on your desktop if you created one.

Or run `remove-tool.bat` from the `Termrig` folder, which does all three.

Your profiles and settings stay in `%USERPROFILE%\.termrig\`. Delete that
folder too if you want to remove them.

## Notes

- **Security prompts:** an app you build on your own PC runs without a
  SmartScreen warning. If you copy it to another PC, Windows may show
  "Windows protected your PC". Choose **More info**, then **Run anyway**. You
  can also right-click the copied folder's `Termrig.exe`, choose
  **Properties**, and check **Unblock**.
- **"Running scripts is disabled":** run the command in step 3 exactly as
  shown. `-ExecutionPolicy Bypass` applies to that one command only.
- **Command-line launcher (optional):** `install-tool.bat` installs a `trig`
  command that starts Termrig from any terminal, and also installs the app as
  in step 3. `remove-tool.bat` removes both.
