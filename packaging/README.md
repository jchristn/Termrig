# Termrig Packaging

This directory contains the first-pass native desktop packaging assets for
Termrig.

## Entry Point

Run packaging tasks from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File packaging/build-packages.ps1 -Target package-all
```

Supported targets:

- `clean`
- `restore`
- `test`
- `publish`
- `package-windows`
- `package-macos`
- `package-ubuntu`
- `package-all`

The script writes published applications under `artifacts/publish/`, packages
under `artifacts/packages/`, and logs under `artifacts/logs/`.

## Host Tooling

The script always requires the .NET SDK for the target framework in
`src/Termrig.App/Termrig.App.csproj`.

Platform packaging has extra host requirements:

- Windows installer: Inno Setup `ISCC.exe` on `PATH`. A portable `.zip` is
  produced even when Inno Setup is unavailable.
- macOS app/DMG: macOS host with `hdiutil`. `iconutil` is needed to regenerate
  `.icns` icons.
- Ubuntu `.deb`: Linux host with `dpkg-deb`. `desktop-file-validate` is used
  when available.

Native desktop packages do not install the legacy `tr` developer command.
