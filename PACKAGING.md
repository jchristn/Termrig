# Termrig Packaging Plan

This document is the end-to-end plan for turning Termrig from a source-built
Avalonia/.NET app into native desktop install artifacts for Windows, macOS, and
Ubuntu.

Use the checkboxes as the working tracker. Add notes, owners, dates, links to
commits, and release artifact URLs directly under each item as work progresses.

## Goals

- [ ] Ship Termrig as a native desktop app on Windows, macOS, and Ubuntu.
- [ ] Keep native desktop installation separate from any optional developer
      command-line workflow.
- [ ] Make the app pinnable to the Windows taskbar, macOS Dock, and Ubuntu dock.
- [ ] Provide stable application identity, icons, shortcuts, uninstall behavior,
      and release documentation.
- [ ] Produce repeatable local and CI packaging commands.
- [ ] Document install, upgrade, uninstall, and troubleshooting flows for users.

## Non-Goals

- [ ] Do not install a short command named `tr`; it conflicts with existing
      commands on Ubuntu.
- [ ] Do not require users to build from source for normal installation.
- [ ] Do not publish to app stores in the first packaging pass unless signing,
      notarization, and review requirements are already available.

## Shared Decisions

- [ ] Confirm product name: `Termrig`.
- [ ] Confirm executable name per platform:
  - Windows: `Termrig.exe`
  - macOS: `Termrig`
  - Linux: `termrig` or `Termrig`
- [ ] Confirm reverse-DNS application ID:
  - Proposed: `com.jchristn.Termrig`
- [ ] Confirm package ID:
  - Windows/MSIX or installer: `com.jchristn.Termrig`
  - macOS bundle ID: `com.jchristn.Termrig`
  - Linux desktop/app ID: `com.jchristn.Termrig`
- [ ] Confirm native desktop packages install only the desktop app and do not
      install `tr`.
- [ ] Decide release channels:
  - [ ] GitHub Releases only
  - [ ] Package repositories later
  - [ ] Store distribution later

## Shared Build Prerequisites

- [ ] Install .NET SDK matching `src/Termrig.App/Termrig.App.csproj`.
- [ ] Verify Avalonia desktop app builds in Release:

```sh
dotnet restore src/Termrig.slnx
dotnet build src/Termrig.slnx --configuration Release
```

- [ ] Verify automated tests before packaging:

```sh
dotnet run --project src/Test.Automated/Test.Automated.csproj --framework net10.0
dotnet test src/Test.Xunit/Test.Xunit.csproj --configuration Release
dotnet test src/Test.Nunit/Test.Nunit.csproj --configuration Release
```

- [ ] Decide publish mode:
  - [ ] Framework-dependent
  - [ ] Self-contained
  - [ ] Single-file
  - [ ] ReadyToRun
  - [ ] Trimmed

Recommended first pass: self-contained, not trimmed, not single-file. Desktop UI
frameworks and reflection-heavy dependencies are easier to validate this way.

- [ ] Add a packaging output folder:

```text
artifacts/
  publish/
  packages/
  logs/
```

- [ ] Add a packaging source folder:

```text
packaging/
  windows/
  macos/
  linux/
  icons/
```

## Application Identity

- [ ] Add a shared constants location for app identity if needed:

```text
Name: Termrig
Publisher: jchristn
AppId: com.jchristn.Termrig
Description: Desktop terminal profile manager
Homepage: https://github.com/jchristn/Termrig
```

- [ ] Ensure all top-level Avalonia windows use the Termrig icon.
- [ ] Investigate whether Avalonia exposes a platform-specific app ID/window
      class setting for Linux and macOS.
- [ ] On Linux, verify the window class with:

```sh
xprop WM_CLASS
```

- [ ] Make the Linux `.desktop` `StartupWMClass` match the actual window class.
- [ ] On Windows, set or verify a stable AppUserModelID if using installer
      shortcuts or MSIX.
- [ ] On macOS, set a stable `CFBundleIdentifier`.

## Icons

- [ ] Keep the source icon in SVG form:

```text
src/Termrig.App/Assets/termrig-logo.svg
```

- [ ] Generate Windows icon:

```text
packaging/icons/termrig.ico
```

- [ ] Generate macOS icon:

```text
packaging/icons/Termrig.icns
```

- [ ] Generate Linux icons:

```text
packaging/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg
packaging/icons/hicolor/16x16/apps/com.jchristn.Termrig.png
packaging/icons/hicolor/32x32/apps/com.jchristn.Termrig.png
packaging/icons/hicolor/48x48/apps/com.jchristn.Termrig.png
packaging/icons/hicolor/64x64/apps/com.jchristn.Termrig.png
packaging/icons/hicolor/128x128/apps/com.jchristn.Termrig.png
packaging/icons/hicolor/256x256/apps/com.jchristn.Termrig.png
packaging/icons/hicolor/512x512/apps/com.jchristn.Termrig.png
```

- [ ] Document the icon generation command.
- [ ] Validate icons on light and dark backgrounds.
- [ ] Replace `ApplicationIcon` in the project if the generated `.ico` should
      become the canonical Windows icon.

## Publish Matrix

- [ ] Create repeatable publish commands for each runtime identifier:

```sh
dotnet publish src/Termrig.App/Termrig.App.csproj \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  --output artifacts/publish/windows-x64

dotnet publish src/Termrig.App/Termrig.App.csproj \
  --configuration Release \
  --runtime osx-x64 \
  --self-contained true \
  --output artifacts/publish/macos-x64

dotnet publish src/Termrig.App/Termrig.App.csproj \
  --configuration Release \
  --runtime osx-arm64 \
  --self-contained true \
  --output artifacts/publish/macos-arm64

dotnet publish src/Termrig.App/Termrig.App.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output artifacts/publish/linux-x64
```

- [ ] Decide whether to also publish:
  - [ ] Windows ARM64
  - [ ] Linux ARM64
  - [ ] macOS universal app bundle
- [ ] Confirm the published app launches on each target OS.
- [ ] Confirm `~/.termrig/` configuration behavior is unchanged.
- [ ] Confirm the detached process behavior in `Program.cs` works correctly
      when launched from shortcuts, desktop files, and Dock/Finder.

## Windows Package Plan

### Windows Artifact Choice

- [ ] Choose first Windows packaging format:
  - [ ] MSIX
  - [ ] WiX MSI
  - [ ] Inno Setup
  - [ ] Squirrel.Windows
  - [ ] Zip archive for portable installs

Recommended first pass: installer plus portable zip. MSIX is cleaner for app
identity but has signing and installation constraints. A classic installer is
usually easier for early releases.

### Windows Installer Contents

- [ ] Install published app under:

```text
%LOCALAPPDATA%\Programs\Termrig\
```

or system-wide:

```text
%ProgramFiles%\Termrig\
```

- [ ] Install `Termrig.exe`.
- [ ] Install required `.dll`, `.json`, `.deps.json`, `.runtimeconfig.json`,
      native libraries, and Avalonia assets from publish output.
- [ ] Install license file.
- [ ] Install README or link to online docs.
- [ ] Create Start Menu shortcut:

```text
Termrig
```

- [ ] Create optional Desktop shortcut.
- [ ] Set shortcut icon to Termrig icon.
- [ ] Set stable AppUserModelID for the shortcut if using a classic installer.
- [ ] Add uninstall entry in Windows Apps & Features.
- [ ] Add upgrade behavior that replaces the previous version cleanly.
- [ ] Ensure uninstall does not delete user data under:

```text
%USERPROFILE%\.termrig\
```

unless the user explicitly chooses that option.

### Windows Signing

- [ ] Decide whether to sign the executable.
- [ ] Decide whether to sign the installer.
- [ ] Store signing certificate securely in CI if automated signing is used.
- [ ] Document unsigned-build warnings for development builds.

### Windows Verification

- [ ] Install on a clean Windows VM.
- [ ] Launch from Start Menu.
- [ ] Launch from Desktop shortcut if enabled.
- [ ] Launch by double-clicking `Termrig.exe`.
- [ ] Pin to taskbar.
- [ ] Close and relaunch from pinned taskbar icon.
- [ ] Verify the pinned icon groups with running windows.
- [ ] Verify multiple launches route commands to the existing instance as
      intended.
- [ ] Verify uninstall removes installed files.
- [ ] Verify uninstall leaves `~/.termrig/` data intact.
- [ ] Verify upgrade from previous package.

## macOS Package Plan

### macOS Artifact Choice

- [ ] Choose first macOS packaging format:
  - [ ] `.app` bundle in `.zip`
  - [ ] `.dmg`
  - [ ] `.pkg`

Recommended first pass: `.app` bundle distributed in a `.dmg`.

### macOS App Bundle

- [ ] Create bundle layout:

```text
Termrig.app/
  Contents/
    Info.plist
    MacOS/
      Termrig
    Resources/
      Termrig.icns
```

- [ ] Copy published `osx-x64` files into `Contents/MacOS/` for Intel build.
- [ ] Copy published `osx-arm64` files into `Contents/MacOS/` for Apple Silicon
      build.
- [ ] Decide whether to build a universal app:
  - [ ] Keep separate `x64` and `arm64` apps.
  - [ ] Merge native executables with `lipo` where applicable.
  - [ ] Validate all native libraries can be merged or carried correctly.
- [ ] Create `Info.plist` with:

```xml
<key>CFBundleName</key>
<string>Termrig</string>
<key>CFBundleDisplayName</key>
<string>Termrig</string>
<key>CFBundleIdentifier</key>
<string>com.jchristn.Termrig</string>
<key>CFBundleExecutable</key>
<string>Termrig</string>
<key>CFBundleIconFile</key>
<string>Termrig</string>
<key>CFBundlePackageType</key>
<string>APPL</string>
<key>LSMinimumSystemVersion</key>
<string>12.0</string>
```

- [ ] Confirm the `CFBundleExecutable` value matches the executable file in
      `Contents/MacOS/`.
- [ ] Confirm the executable has execute permissions:

```sh
chmod +x Termrig.app/Contents/MacOS/Termrig
```

- [ ] Confirm Dock displays the Termrig icon.
- [ ] Confirm app can be pinned to Dock.
- [ ] Confirm app relaunches from Dock after closing.

### macOS Signing and Notarization

- [ ] Decide whether to distribute unsigned development builds.
- [ ] Acquire Apple Developer ID certificate for public releases.
- [ ] Code sign the app bundle:

```sh
codesign --deep --force --options runtime --sign "Developer ID Application: ..." Termrig.app
```

- [ ] Verify signature:

```sh
codesign --verify --deep --strict --verbose=2 Termrig.app
spctl --assess --type execute --verbose Termrig.app
```

- [ ] Notarize release builds:

```sh
xcrun notarytool submit Termrig.dmg --keychain-profile termrig --wait
xcrun stapler staple Termrig.dmg
```

- [ ] Document Gatekeeper behavior for unsigned local builds.

### macOS DMG

- [ ] Create a DMG containing:

```text
Termrig.app
Applications symlink
LICENSE.md
```

- [ ] Add a background image only if desired.
- [ ] Test drag-to-Applications install.
- [ ] Test running from inside the DMG and from `/Applications`.
- [ ] Verify uninstall instructions:

```text
Delete /Applications/Termrig.app.
User data remains in ~/.termrig/.
```

### macOS Verification

- [ ] Install on clean Intel macOS VM or machine.
- [ ] Install on clean Apple Silicon macOS machine.
- [ ] Launch from Finder.
- [ ] Launch from Spotlight.
- [ ] Launch from Dock.
- [ ] Pin to Dock.
- [ ] Verify icon grouping in Dock.
- [ ] Verify terminal tabs can launch expected shells.
- [ ] Verify crash logs are written under `~/.termrig/crashes/`.
- [ ] Verify upgrade by replacing the app bundle.

## Ubuntu Package Plan

### Ubuntu Artifact Choice

- [ ] Choose first Ubuntu packaging format:
  - [ ] `.deb`
  - [ ] AppImage
  - [ ] Flatpak
  - [ ] Snap
  - [ ] `.tar.gz` portable archive

Recommended first pass: `.deb` plus `.tar.gz`. The `.deb` provides native
launcher integration; the archive is useful for debugging and non-Debian Linux.

### Linux Install Layout

- [ ] Install application files under:

```text
/opt/termrig/
```

- [ ] Install desktop file:

```text
/usr/share/applications/com.jchristn.Termrig.desktop
```

- [ ] Install icons:

```text
/usr/share/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg
/usr/share/icons/hicolor/256x256/apps/com.jchristn.Termrig.png
```

- [ ] Install license:

```text
/usr/share/doc/termrig/LICENSE.md
```

- [ ] Ensure uninstall does not delete:

```text
~/.termrig/
```

### Linux Desktop File

- [ ] Create `packaging/linux/com.jchristn.Termrig.desktop`:

```ini
[Desktop Entry]
Type=Application
Name=Termrig
Comment=Desktop terminal profile manager
Exec=/opt/termrig/Termrig.App
Icon=com.jchristn.Termrig
Terminal=false
Categories=System;TerminalEmulator;Utility;
StartupNotify=true
StartupWMClass=Termrig.App
```

- [ ] Validate desktop file:

```sh
desktop-file-validate packaging/linux/com.jchristn.Termrig.desktop
```

- [ ] Verify `StartupWMClass` on Ubuntu X11:

```sh
xprop WM_CLASS
```

- [ ] Verify app identity on Ubuntu Wayland.
- [ ] Adjust Avalonia/Linux app ID or desktop file if dock grouping is wrong.

### Linux Launch Command

- [ ] Do not install `/usr/bin/tr`, `/usr/local/bin/tr`, or any other short
      `tr` command from native packages.
- [ ] Do not require a shell command wrapper for Ubuntu desktop integration.
- [ ] Launch the app from the `.desktop` file using the fully-qualified packaged
      executable path:

```ini
Exec=/opt/termrig/Termrig.App
```

- [ ] Confirm the executable has execute permissions:

```sh
chmod +x /opt/termrig/Termrig.App
```

- [ ] Confirm launching through the `.desktop` file detaches or returns control
      as intended.
- [ ] If a future CLI is needed, use a non-conflicting command name such as
      `termrig` and track it as a separate feature from native desktop
      packaging.

### Debian Package

- [ ] Create package staging directory:

```text
artifacts/packages/deb/termrig/
  DEBIAN/control
  opt/termrig/
  usr/share/applications/com.jchristn.Termrig.desktop
  usr/share/icons/hicolor/...
  usr/share/doc/termrig/
```

- [ ] Create `DEBIAN/control`:

```text
Package: termrig
Version: 0.1.0
Section: utils
Priority: optional
Architecture: amd64
Maintainer: Termrig Maintainers
Description: Desktop terminal profile manager
```

- [ ] Add dependencies only if the published output is not self-contained.
- [ ] Add `postinst` script to update desktop/icon caches where appropriate.
- [ ] Add `postrm` script to update desktop/icon caches where appropriate.
- [ ] Build package:

```sh
dpkg-deb --build artifacts/packages/deb/termrig artifacts/packages/termrig_0.1.0_amd64.deb
```

- [ ] Inspect package:

```sh
dpkg-deb --info artifacts/packages/termrig_0.1.0_amd64.deb
dpkg-deb --contents artifacts/packages/termrig_0.1.0_amd64.deb
```

- [ ] Install package:

```sh
sudo apt install ./artifacts/packages/termrig_0.1.0_amd64.deb
```

- [ ] Uninstall package:

```sh
sudo apt remove termrig
```

### Ubuntu Verification

- [ ] Install on clean Ubuntu LTS VM.
- [ ] Launch from terminal with fully-qualified path:

```sh
/opt/termrig/Termrig.App
```

- [ ] Launch from application menu.
- [ ] Add to dock.
- [ ] Close and relaunch from dock.
- [ ] Verify running window groups with pinned dock icon.
- [ ] Verify app icon renders in application menu and dock.
- [ ] Verify terminal tabs can launch `bash`.
- [ ] Verify shell discovery works as expected.
- [ ] Verify `~/.termrig/` profile storage works.
- [ ] Verify crash logs are written under `~/.termrig/crashes/`.
- [ ] Verify uninstall removes installed files.
- [ ] Verify uninstall leaves user data intact.
- [ ] Verify upgrade from previous `.deb`.

## Command-Line Installation

- [ ] Native desktop packages must not install `tr`.
- [ ] Native desktop packages must not depend on `tr`.
- [ ] Native desktop packages should launch using fully-qualified executable
      paths in shortcuts, desktop files, and bundle metadata.
- [ ] Review `install-tool.sh` and `install-tool.bat`; if they remain in the
      repo, clearly mark them as legacy/developer-only or rename the command to
      avoid Ubuntu conflicts.
- [ ] Document the difference between:
  - desktop app install
  - optional developer command install, if retained
  - source checkout quickstart
- [ ] Ensure command routing to a running instance works from native package
      launches without requiring a short command on `PATH`.

## Packaging Scripts

- [ ] Add a single build entry point:

```text
packaging/build-packages.ps1
```

- [ ] Add shell equivalent if needed:

```text
packaging/build-packages.sh
```

- [ ] Script should support:
  - [ ] clean
  - [ ] restore
  - [ ] test
  - [ ] publish
  - [ ] package-windows
  - [ ] package-macos
  - [ ] package-ubuntu
  - [ ] package-all
- [ ] Script should write logs under:

```text
artifacts/logs/
```

- [ ] Script should fail on first packaging error.
- [ ] Script should print final artifact paths.
- [ ] Script should not require admin rights except when explicitly installing a
      package for verification.

## CI Release Pipeline

- [ ] Add GitHub Actions workflow for pull request validation:
  - [ ] restore
  - [ ] build
  - [ ] tests
  - [ ] packaging dry run where possible
- [ ] Add GitHub Actions workflow for tagged releases:
  - [ ] build Windows package on Windows runner
  - [ ] build macOS package on macOS runner
  - [ ] build Ubuntu package on Ubuntu runner
  - [ ] upload artifacts
  - [ ] create GitHub Release draft
  - [ ] attach checksums
  - [ ] attach release notes
- [ ] Generate SHA256 checksums:

```text
termrig-windows-x64-installer.exe.sha256
termrig-macos-arm64.dmg.sha256
termrig-macos-x64.dmg.sha256
termrig_0.1.0_amd64.deb.sha256
```

- [ ] Store signing credentials securely.
- [ ] Ensure unsigned CI artifacts are clearly labeled if signing is unavailable.

## Versioning

- [ ] Keep version in `src/Termrig.App/Termrig.App.csproj`.
- [ ] Decide whether package scripts read version from the project file.
- [ ] Ensure all package metadata uses the same version:
  - [ ] assembly version
  - [ ] file version
  - [ ] NuGet package version
  - [ ] Windows installer version
  - [ ] macOS bundle version
  - [ ] Debian package version
  - [ ] GitHub release tag
- [ ] Define release tag format:

```text
v0.1.0
```

## Documentation Plan

### README Updates

- [ ] Add an "Install" section before "Quickstart".
- [ ] Document Windows installer install flow.
- [ ] Document macOS DMG install flow.
- [ ] Document Ubuntu `.deb` install flow.
- [ ] Keep source checkout quickstart for contributors.
- [ ] Remove or revise the `tr` global tool section so users are not instructed
      to install a conflicting Ubuntu command.
- [ ] Add uninstall instructions per platform.
- [ ] Add upgrade instructions per platform.
- [ ] Add troubleshooting section for dock/taskbar pinning.

### Release Notes

- [ ] Add release note template:

```text
## Termrig 0.1.0

### Downloads
- Windows x64 installer
- macOS Apple Silicon DMG
- macOS Intel DMG
- Ubuntu amd64 DEB
- Portable archives

### Install
...

### Known Issues
...

### Checksums
...
```

- [ ] Document platform support level:
  - [ ] Tested
  - [ ] Expected to work
  - [ ] Unsupported

### User Documentation

- [ ] Create docs page or README section for:
  - [ ] first launch
  - [ ] creating profiles
  - [ ] saving profiles
  - [ ] opening a workspace
  - [ ] where user data is stored
  - [ ] where crash logs are stored
  - [ ] how to report bugs
- [ ] Add screenshots for each platform after packages exist.
- [ ] Document how to pin:
  - [ ] Windows taskbar
  - [ ] macOS Dock
  - [ ] Ubuntu dock
- [ ] Document known limitations for unsigned builds.

### Developer Documentation

- [ ] Add `docs/packaging-windows.md`.
- [ ] Add `docs/packaging-macos.md`.
- [ ] Add `docs/packaging-ubuntu.md`.
- [ ] Include required tooling.
- [ ] Include local package build commands.
- [ ] Include local install/test/uninstall commands.
- [ ] Include signing/notarization instructions where applicable.
- [ ] Include common failure modes and fixes.

## Manual Release Checklist

- [ ] Update changelog.
- [ ] Update version in project file.
- [ ] Run full tests.
- [ ] Build packages.
- [ ] Install each package on a clean machine or VM.
- [ ] Run platform verification checklist.
- [ ] Generate checksums.
- [ ] Draft release notes.
- [ ] Upload artifacts to GitHub Releases.
- [ ] Verify downloads from GitHub Releases.
- [ ] Install from downloaded artifacts.
- [ ] Publish release.
- [ ] Create follow-up issues for any deferred packaging defects.

## First Implementation Milestone

- [x] Add `packaging/` directory.
  - Added `packaging/README.md` and `packaging/build-packages.ps1`.
- [ ] Add generated platform icons.
  - Added Windows `.ico` and Linux scalable SVG packaging icons. Raster Linux
    PNGs and macOS `.icns` generation are documented and remain release-host
    work.
- [x] Add Ubuntu `.desktop` file.
  - Added `packaging/linux/com.jchristn.Termrig.desktop`.
- [x] Add Ubuntu `.deb` staging script.
  - Added `package-ubuntu` target in `packaging/build-packages.ps1`.
- [x] Add macOS `Info.plist` template.
  - Added `packaging/macos/Info.plist`.
- [x] Add macOS `.app` bundle script.
  - Added `package-macos` target in `packaging/build-packages.ps1`.
- [x] Add Windows installer decision and prototype script.
  - Added portable zip output and `packaging/windows/Termrig.iss` for Inno
    Setup.
- [x] Add README install/uninstall documentation.
- [ ] Verify Ubuntu dock pinning end to end.
- [ ] Verify macOS Dock pinning end to end.
- [ ] Verify Windows taskbar grouping end to end.

## Open Questions

- [ ] What publisher identity should be used for package metadata?
- [ ] Is `com.jchristn.Termrig` the final app ID?
- [ ] Should Termrig install for the current user or all users by default?
- [ ] Should a future optional CLI use `termrig`, another non-conflicting name,
      or no CLI at all?
- [ ] Should the first Linux package target only Ubuntu LTS?
- [ ] Should macOS builds be separate architecture downloads or a universal app?
- [ ] Will public releases be signed immediately or after packaging is stable?
- [ ] Should user data removal be offered during uninstall?
- [ ] Should auto-update be considered later, and if so, per-platform or
      app-level?
