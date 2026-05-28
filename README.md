<p align="center">
  <img src="src/Termrig.App/Assets/termrig-logo.svg" alt="Termrig logo" width="128" height="128" />
</p>

# Termrig

**NOTE: Termrig is in ALPHA v0.1.0 and subject to change.**

Termrig is a desktop profile manager, allowing you to create profiles consisting of groups of tabs, each with their own configuration. Termrig is designed to allow developers to quickly open a collection of tabs together as an atomic unit to rapidly reproduce workspaces for development and operational workflows.

## MVP Features

- Windows shell support for `cmd.exe` and PowerShell.
- macOS/Linux shell support for `bash`.
- Profile management from the main window: create, delete, rename, save, and apply a global color scheme.
- Per-tab settings: name, shell, starting directory, startup script, font, and optional color override.
- Tabbed terminal workspace with a `Save profile` action that overwrites the existing profile.
- Closing a workspace tab closes only the live terminal session; it does not remove the tab from the saved profile.
- Profiles are stored as JSON under `~/.termrig/profiles.json`.

## Build

```powershell
dotnet restore src/Termrig.slnx
dotnet build src/Termrig.slnx
dotnet run --project src/Test.Automated/Test.Automated.csproj --framework net10.0
dotnet test src/Test.Xunit/Test.Xunit.csproj
dotnet test src/Test.Nunit/Test.Nunit.csproj
```

## Run

```powershell
dotnet run --project src/Termrig.App/Termrig.App.csproj
```

Termrig targets .NET 10 and uses Avalonia for the desktop UI.

## Issues and Enhancements

Use the GitHub repository to report problems or suggest improvements:

- File bugs and enhancement requests in [GitHub Issues](https://github.com/jchristn/Termrig/issues).
- Use [GitHub Discussions](https://github.com/jchristn/Termrig/discussions) for questions, ideas, and design discussion.
