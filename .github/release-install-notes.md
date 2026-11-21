## Installing

> [!IMPORTANT]
> **These builds are not code-signed, so Windows and macOS block the first launch.** This is expected and does not
> mean the download is damaged. The steps to allow Termrig are below. To confirm a file is the one published here,
> check it against `SHA256SUMS`.

Termrig is free, open-source software and is not code-signed (signing certificates are an ongoing cost we choose not
to pass on). Termrig is in alpha and subject to change.

### macOS: "Termrig" Not Opened

macOS shows *"Termrig" Not Opened. Apple could not verify "Termrig" is free of malware that may harm your Mac or
compromise your privacy.* with **Move to Trash** and **Done** buttons. To open it anyway:

1. Click **Done**. Do not click Move to Trash.
2. Open **System Settings > Privacy & Security** and scroll down to **Security**.
3. Next to *"Termrig" was blocked to protect your Mac*, click **Open Anyway**. The button only appears for about an
   hour after the blocked attempt; if it is missing, try to open Termrig again first.
4. Click **Open Anyway** again in the dialog that follows and enter your password. You only do this once.

Or skip the prompt in Terminal:

```bash
xattr -dr com.apple.quarantine ~/Downloads/termrig-{version}-osx-arm64.pkg   # before opening the .pkg
xattr -dr com.apple.quarantine /Applications/Termrig.app                     # after copying the app from the .dmg
sudo installer -pkg ~/Downloads/termrig-{version}-osx-arm64.pkg -target /    # installs the .pkg with no prompt at all
```

On macOS 15 and later, Control-click > **Open** no longer bypasses this prompt.

### Windows: "Windows protected your PC"

Click **More info**, then **Run anyway**. If your browser warns that the file *isn't commonly downloaded*, choose
**Keep** (in Edge: the **...** menu on the download, **Keep**, then **Show more > Keep anyway**). For the `.zip`, run
`Unblock-File` on it (or right-click > Properties > Unblock) before extracting.

### Linux

No prompt. `sudo apt install ./termrig_{version}_amd64.deb`, `sudo dnf install ./termrig-{version}-1.x86_64.rpm`, or
`chmod +x` the AppImage and run it. Keep the `./` in the apt command, or apt searches its repositories instead. The
packages pull in the X11 and fontconfig libraries Termrig's window needs; the AppImage expects them to be installed
already, and older distributions need `libfuse2` to run it.

### Which file to download

| Platform | File |
| --- | --- |
| Windows (x64) | `termrig-{version}-win-x64-setup.exe` |
| Windows (ARM64) | `termrig-{version}-win-arm64-setup.exe` |
| macOS (Apple Silicon) | `termrig-{version}-osx-arm64.pkg` (or `.dmg`) |
| macOS (Intel) | `termrig-{version}-osx-x64.pkg` (or `.dmg`) |
| Debian / Ubuntu | `termrig_{version}_amd64.deb` (or `_arm64.deb`) |
| Fedora / RHEL | `termrig-{version}-1.x86_64.rpm` (or `.aarch64.rpm`) |
| Any Linux | `termrig-{version}-x86_64.AppImage` (or `-aarch64.AppImage`) |
| Scripts and containers | `termrig-{version}-<rid>.tar.gz` / `termrig-{version}-win-<arch>.zip` |

Not sure which Mac you have? Apple menu > **About This Mac**: "Chip: Apple M..." is Apple Silicon, "Processor:
Intel" is Intel.

Every installer adds Termrig to the Start menu, Applications folder, or application menu, so you can pin it to the
taskbar or Dock, and adds the `trig` command to `PATH` (the `.dmg` only copies the app; use the `.pkg` for `trig`).
Your profiles and settings stay in `~/.termrig` and are never removed by an uninstall.

Full instructions, including uninstalling: [INSTALL.md](https://github.com/jchristn/Termrig/blob/main/INSTALL.md). To
build from source, use the **Source code** archives below and follow
[Run from Source](https://github.com/jchristn/Termrig#run-from-source-development).
