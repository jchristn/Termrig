# Installing Termrig

Download Termrig from the [latest release](https://github.com/jchristn/Termrig/releases/latest). Every installer is
self-contained, so you don't need .NET. Each one puts Termrig in the Start menu, Applications folder, or application
menu, where you can pin it to the taskbar or Dock, and adds the `trig` command to `PATH`.

> [!IMPORTANT]
> **Termrig is free, open-source software and is not code-signed** (signing certificates are an ongoing cost we
> choose not to pass on). Windows and macOS warn that the developer is unidentified the first time you open it. That
> warning doesn't mean the download is damaged; to confirm a file is the one published, check it against
> `SHA256SUMS` (see [Verify a download](#verify-a-download)). The steps to get past each warning are below.

Termrig is in alpha and subject to change. To build and install from source instead, see
[INSTALLING_ON_MAC.md](INSTALLING_ON_MAC.md), [INSTALLING_ON_WINDOWS.md](INSTALLING_ON_WINDOWS.md), and
[INSTALLING_ON_LINUX.md](INSTALLING_ON_LINUX.md).

## Which file to download

Replace `<version>` with the release version, for example `0.1.1`.

| Platform | File |
| --- | --- |
| Windows (x64) | `termrig-<version>-win-x64-setup.exe` |
| Windows (ARM64) | `termrig-<version>-win-arm64-setup.exe` |
| macOS (Apple Silicon) | `termrig-<version>-osx-arm64.pkg` (or `.dmg`) |
| macOS (Intel) | `termrig-<version>-osx-x64.pkg` (or `.dmg`) |
| Debian / Ubuntu | `termrig_<version>_amd64.deb` (or `_arm64.deb`) |
| Fedora / RHEL | `termrig-<version>-1.x86_64.rpm` (or `.aarch64.rpm`) |
| Any Linux | `termrig-<version>-x86_64.AppImage` (or `-aarch64.AppImage`) |
| Scripts and containers | `termrig-<version>-<rid>.tar.gz` / `termrig-<version>-win-<arch>.zip` |

Not sure which Mac you have? Apple menu > **About This Mac**: "Chip: Apple M..." is Apple Silicon, "Processor:
Intel" is Intel.

## Windows

1. Download the setup `.exe`. If your browser warns that the file *isn't commonly downloaded*, choose **Keep** (in
   Edge: the **...** menu on the download, **Keep**, then **Show more > Keep anyway**).
2. Run it. Windows shows **Windows protected your PC** (*Microsoft Defender SmartScreen prevented an unrecognized app
   from starting.*) with only a **Don't run** button. Click **More info**, then **Run anyway**.
3. Accept the license and finish the wizard. Termrig installs in `C:\Program Files\Termrig`, adds a Start menu
   shortcut (and a desktop shortcut if you tick the box), and adds its folder to the system `PATH`.

Open a new terminal and run `trig` to start Termrig, or open it from the Start menu and pin it to the taskbar.

**Portable `.zip`:** unblock the archive before extracting it, or every extracted file inherits the download mark and
SmartScreen blocks `Termrig.exe` too:

```powershell
Unblock-File .\termrig-<version>-win-x64.zip
Expand-Archive .\termrig-<version>-win-x64.zip -DestinationPath "$env:LOCALAPPDATA\Programs\Termrig"
```

The archive holds `Termrig.exe`, `trig.cmd`, the license, and the bundled font licenses. Add the folder to your `PATH`
to use `trig`.

If Microsoft Defender (not SmartScreen) quarantines the file as malware, that is a false positive that self-extracting
single-file programs sometimes trigger. Please report it in [Issues](https://github.com/jchristn/Termrig/issues); don't
turn Defender off.

**Uninstall:** **Settings > Apps > Installed apps > Termrig > Uninstall**. This removes the program, its shortcuts, and
its `PATH` entry.

## macOS

The `.pkg` is the recommended installer: it installs `Termrig.app` in `/Applications` and creates
`/usr/local/bin/trig`. The `.dmg` is a drag-to-Applications copy and does not add `trig`.

On macOS 15 (Sequoia) and later, opening the downloaded `.pkg` or app shows:

> **"Termrig" Not Opened**
> Apple could not verify "Termrig" is free of malware that may harm your Mac or compromise your privacy.
> [Move to Trash] [Done]

(Older versions say *"Termrig" cannot be opened because it is from an unidentified developer*, and the `.pkg` prompt
is named after the file.) To open it anyway:

1. Click **Done**. Do not click Move to Trash.
2. Open **System Settings > Privacy & Security** and scroll down to **Security**.
3. Next to *"Termrig" was blocked to protect your Mac*, click **Open Anyway**. The button only appears for about an
   hour after the blocked attempt; if it is missing, try to open Termrig again first.
4. Click **Open Anyway** again in the dialog that follows and enter your password. You only do this once.

On macOS 15 and later, Control-click > **Open** no longer bypasses this prompt, whatever older guides say.

Or skip the prompt in Terminal:

```sh
xattr -dr com.apple.quarantine ~/Downloads/termrig-<version>-osx-arm64.pkg   # before opening the .pkg
sudo installer -pkg ~/Downloads/termrig-<version>-osx-arm64.pkg -target /    # installs the .pkg with no prompt at all
xattr -dr com.apple.quarantine /Applications/Termrig.app                     # after copying the app from the .dmg
```

The `.pkg` shows a welcome page and the license, which you accept before it installs. Open Termrig from Launchpad or
Spotlight and keep it in the Dock, or run `trig` in a new terminal.

**From the `.dmg`:** drag Termrig onto Applications. The disk image also holds `If Termrig won't open.txt` with the
steps above. To add `trig` yourself:

```sh
sudo mkdir -p /usr/local/bin && sudo ln -sf /Applications/Termrig.app/Contents/MacOS/Termrig /usr/local/bin/trig
```

**`.tar.gz`:** holds the bare `Termrig` executable. A file downloaded with `curl` has no quarantine flag; one
downloaded in a browser needs `xattr -d com.apple.quarantine ./Termrig`. The bare executable runs, but without the app
bundle it gets no Dock icon of its own, so prefer the `.pkg` for everyday use.

**Uninstall:**

```sh
sudo /Applications/Termrig.app/Contents/Resources/uninstall.sh
```

This removes `Termrig.app`, `/usr/local/bin/trig`, and the installer receipt. For a `.dmg` install, drag Termrig from
Applications to the Trash.

## Linux

Linux shows no prompt for unsigned packages.

```sh
sudo apt install ./termrig_<version>_amd64.deb         # Debian, Ubuntu, and WSL 2 (WSLg)
sudo dnf install ./termrig-<version>-1.x86_64.rpm      # Fedora, RHEL
```

Keep the `./`, or apt searches its repositories instead of installing the file. Use `apt install` rather than
`dpkg -i`: only apt installs the X11, fontconfig, and ICU libraries the package depends on. The packages install
Termrig in `/opt/termrig`, link `/usr/bin/trig` to it, and add a menu entry and icons, so you can open Termrig from
the application menu and pin it to the dock.

**AppImage:** `chmod +x termrig-<version>-x86_64.AppImage`, then run it. Older distributions need `libfuse2` to mount
it. The AppImage does not install the desktop libraries; if Termrig stops with
`Unable to load shared library 'libICE.so.6'`, install them as described in
[Desktop libraries](INSTALLING_ON_LINUX.md#desktop-libraries).

**`.tar.gz`:** holds the bare `Termrig` executable and the font licenses. Run it with the desktop libraries
installed.

**Uninstall:** `sudo apt remove termrig` or `sudo dnf remove termrig`. Delete the AppImage file to remove it.

## Verify a download

Each release has a `SHA256SUMS` file covering every asset. Download it into the same folder as your files, then:

```sh
shasum -a 256 -c SHA256SUMS --ignore-missing      # macOS
sha256sum -c SHA256SUMS --ignore-missing          # Linux
```

```powershell
(Get-FileHash .\termrig-<version>-win-x64-setup.exe -Algorithm SHA256).Hash   # Windows: compare with SHA256SUMS
```

## Moving from a source build

The build-from-source scripts (`install-tool`, `packaging/*/build-app.*`) install Termrig under a different
application ID and in different places (`~/.local/share/termrig`, `%LOCALAPPDATA%\Programs\Termrig`). Run
`remove-tool` from your clone before installing a release package, or you end up with two copies and two menu
entries. The .NET global tool `trig` from `install-tool` also comes first on `PATH` if `~/.dotnet/tools` is listed
before the installer's directory.

## Your data

Profiles, color schemes, settings, and crash logs live in `~/.termrig` (`%USERPROFILE%\.termrig` on Windows).
Installing, upgrading, and uninstalling never touch it.

## Building the installers

Maintainers: see [RELEASING.md](RELEASING.md). `./build-installers.sh` (macOS, Linux) and `build-installers.bat`
(Windows) build the installers your OS can produce into `installers/<version>/`.
