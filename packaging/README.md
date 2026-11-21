# Termrig Packaging

Release installers (Windows `.exe`/`.zip`, macOS `.pkg`/`.dmg`/`.tar.gz`, Linux `.deb`/`.rpm`/AppImage/`.tar.gz`)
are built by `src/Termrig.Publisher` from [`publisher.json`](../publisher.json), with `build-installers.sh` or
`build-installers.bat` at the repository root and in CI by `.github/workflows/release.yml`. See
[RELEASING.md](../RELEASING.md) and [INSTALL.md](../INSTALL.md).

This directory holds the build-from-source install scripts and their assets:

- macOS: `packaging/macos/build-app.sh` and `Info.plist` (INSTALLING_ON_MAC.md)
- Windows: `packaging/windows/build-app.ps1` (INSTALLING_ON_WINDOWS.md)
- Linux: `packaging/linux/build-app.sh` and `com.jchristn.Termrig.desktop` (INSTALLING_ON_LINUX.md)
- Icons: `packaging/icons/` (see its README)

Source installs use the application ID `com.jchristn.Termrig`; the release installers use `ai.latticetech.termrig`.
Remove a source install with `remove-tool` before installing a release package, or you get two menu entries.
