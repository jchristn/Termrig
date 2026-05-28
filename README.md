# Termrig

Termrig is an Avalonia desktop terminal profile manager. It opens a profile as a multi-tab terminal workspace and launches each tab into a real PTY-backed terminal session.

## MVP Features

- Windows shell support for `cmd.exe` and PowerShell.
- macOS/Linux shell support for `bash`.
- Profile management from the main window: create, delete, rename, save, and apply a global color scheme.
- Per-tab settings: name, shell, starting directory, startup script, and optional color override.
- Tabbed terminal workspace with a `Save profile` action that overwrites the existing profile.
- Profiles are stored as JSON under `~/.termrig/profiles.json`.

## Build

```powershell
dotnet restore src/Termrig.slnx
dotnet build src/Termrig.slnx
dotnet test src/Test.Xunit/Test.Xunit.csproj
```

## Run

```powershell
dotnet run --project src/Termrig.App/Termrig.App.csproj
```

Termrig targets .NET 10 and uses Avalonia for the desktop UI.
