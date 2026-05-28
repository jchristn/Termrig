<p align="center">
  <img src="src/Termrig.App/Assets/termrig-logo.svg" alt="Termrig logo" width="128" height="128" />
</p>

# Termrig

**NOTE: Termrig is in ALPHA v0.1.0 and subject to change.**

Termrig is a desktop profile manager, allowing you to create profiles consisting of groups of tabs, each with their own configuration. Termrig is designed to allow developers to quickly open a collection of tabs together as an atomic unit to rapidly reproduce workspaces for development and operational workflows.

## Quickstart

Windows:

```powershell
go.bat
```

Mac/Linux:

```sh
chmod +x go.sh && go.sh
```

If your shell does not resolve scripts from the current directory, run `chmod +x go.sh && ./go.sh`.

## Features

- Create reusable terminal profiles made up of multiple tabs.
- Open a profile as a tabbed terminal workspace, with each tab launched into a real PTY-backed terminal session.
- Windows shell support for `cmd.exe` and PowerShell.
- macOS/Linux shell support for `bash`.
- Configure each tab with a name, shell, starting directory, startup script, font override, and optional color scheme override.
- Manage profiles from the main window: create, delete, rename, save, reorder tabs, and apply profile-wide defaults.
- Save profile changes directly from the workspace window.
- Add transient workspace tabs and optionally add them back to the active profile.
- Rename workspace tabs from the tab strip.
- Close live workspace tabs without removing them from the saved profile.
- Manage global color schemes, including adding custom schemes and resetting to built-in defaults.
- Apply profile-wide fonts and color schemes, with tab-level overrides when needed.
- Store profiles and color schemes as JSON under `~/.termrig/`.

## Benefits

- Recreate complex terminal workspaces in one action.
- Keep project-specific directories, shells, startup scripts, fonts, and colors attached to the profile that needs them.
- Separate saved profile configuration from live workspace activity, so closing a terminal tab does not destroy the saved profile.
- Use the same workflow across Windows, macOS, and Linux with host-appropriate shell support.
- Keep configuration portable and inspectable through JSON files under `~/.termrig/`.

## Use Cases

- Open a full development environment with one tab per service, repository, or tool.
- Keep operational runbooks as repeatable terminal profiles.
- Switch between client, project, or incident workspaces without manually recreating directories and shells.
- Start shells with profile-specific setup commands or scripts.
- Maintain different font and color settings for focused workflows, accessibility needs, or demos.

## Configuration

Termrig stores user data under `~/.termrig/`.

- `profiles.json` stores saved terminal profiles and their tabs.
- `color-schemes.json` stores editable global color schemes.

## Build and Test

```powershell
dotnet restore src/Termrig.slnx
dotnet build src/Termrig.slnx
dotnet run --project src/Test.Automated/Test.Automated.csproj --framework net10.0
dotnet test src/Test.Xunit/Test.Xunit.csproj
dotnet test src/Test.Nunit/Test.Nunit.csproj
```

Termrig targets .NET 10 and uses Avalonia for the desktop UI.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).

## License

Termrig is licensed under the MIT license. See [LICENSE.md](LICENSE.md).

## Issues and Discussions

Use the GitHub repository to report problems or suggest improvements:

- File bugs and enhancement requests in [GitHub Issues](https://github.com/jchristn/Termrig/issues).
- Use [GitHub Discussions](https://github.com/jchristn/Termrig/discussions) for questions, ideas, and design discussion.
