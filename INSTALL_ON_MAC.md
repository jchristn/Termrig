# Installing Termrig on macOS

Building from source on macOS gives you two things:

- `Termrig.app` in `~/Applications`. Launch it from Finder, Spotlight,
  Launchpad, or the Dock.
- The `trig` command, a .NET global tool that starts Termrig from a terminal.

Pin `Termrig.app` to the Dock, not `trig`. The Dock can only show an icon for a
pinned item when that item is an app bundle. A pinned `trig` shows the Termrig
logo while it is running and the generic "exec" icon once it quits.

## Requirements

- macOS 12 or later, on Apple silicon or Intel.
- The .NET SDK version targeted by `src/Termrig.App/Termrig.App.csproj`
  (currently .NET 10).
- The Xcode command line tools (`xcode-select --install`), which provide `sips`,
  `iconutil`, and `codesign`.

## Install

```sh
git clone https://github.com/jchristn/Termrig
cd Termrig
chmod +x install-tool.sh && ./install-tool.sh
```

This script:

1. Builds and installs the `trig` global tool.
2. Runs `packaging/macos/build-app.sh`, which:
   - publishes a self-contained build for your Mac's architecture
     (`osx-arm64` or `osx-x64`)
   - generates `Termrig.icns` from `src/Termrig.App/Assets/termrig-logo.png`
   - assembles the bundle using `packaging/macos/Info.plist`
   - signs the bundle ad hoc
   - installs it to `~/Applications/Termrig.app`

To rebuild everything from scratch, run `./reinstall-tool.sh`.

### Build only the app

```sh
./packaging/macos/build-app.sh                  # installs to ~/Applications
./packaging/macos/build-app.sh /Applications    # or choose another folder
```

The script stages the bundle under `artifacts/macos-app/<rid>/` before copying
it to the install folder.

## Add Termrig to the Dock

1. If an old `trig` tile with the "exec" icon is in the Dock, right-click it and
   choose **Options** and uncheck **Keep in Dock**.
2. Open `~/Applications/Termrig.app` (for example, with `open
   ~/Applications/Termrig.app`).
3. Right-click the Termrig Dock tile and choose **Options → Keep in Dock**.

## Notes

- **Running from the app vs. the terminal:** `trig` detaches from the terminal
  so you can keep using it. The app bundle sets `TERMRIG_FOREGROUND=1` through
  `LSEnvironment` in `Info.plist`, so a Dock or Finder launch stays a single
  process.
- **Updating:** rerun `./install-tool.sh` or `./packaging/macos/build-app.sh`.
  Quit Termrig first, because the script replaces the installed bundle.
- **Gatekeeper:** a bundle you build locally is not quarantined, so it opens
  without a prompt. If you copy it to another Mac, that Mac may block it.
  Right-click the app and choose **Open** to allow it.
- **Stale icon:** if Finder or the Dock still shows an old icon after a rebuild,
  run `killall Dock`.

## Uninstall

```sh
./remove-tool.sh
```

This removes the `trig` global tool and `~/Applications/Termrig.app`. Your
profiles and settings in `~/.termrig/` stay in place.
