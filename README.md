<p align="center">
  <img src="src/Termrig.App/Assets/termrig-logo.svg" alt="Termrig logo" width="128" height="128" />
</p>

# Termrig

**NOTE: Termrig is in ALPHA v0.1.0 and subject to change.**

Is it pretty?  NO.  Is it flashy?  NO.  Is it a runway model?  NO.

It's none of that.  It's Termrig, a desktop terminal profile manager, allowing you to create profiles with groups of tabs, each with their own configuration and startup scripts.  Termrig is designed to allow you to quickly open a collection of terminal tabs together as a grouped atomic unit to rapidly reproduce workspaces for development and operational workflows.

Why did I build it and what problem am I trying to solve? I wanted a simple way to restart a series of tabs for software development against a given software asset, i.e. open a terminal for the source directory, spawn an agent harness in another, another terminal tab for the logs directory, and yet another for the docker directory.  I set up a profile with a series of tabs defined within, and launch the profile.

## Quickstart

Windows:

```powershell
git clone https://github.com/jchristn/Termrig
cd Termrig
go.bat
```

Mac/Linux:

```sh
git clone https://github.com/jchristn/Termrig
cd Termrig
chmod +x go.sh && ./go.sh
```

## Global Command

Termrig can be installed from the repository as a .NET global tool named `tr`.

Install on Windows:

```powershell
git clone https://github.com/jchristn/Termrig
cd Termrig
install-tool.bat
```

Install on Mac/Linux:

```sh
git clone https://github.com/jchristn/Termrig
cd Termrig
chmod +x install-tool.sh && ./install-tool.sh
```

After installation, run Termrig from any terminal:

```sh
tr
```

The `tr` command starts the Termrig desktop app and relinquishes the terminal so you can keep using that terminal window.

Remove on Windows:

```powershell
remove-tool.bat
```

Remove on Mac/Linux:

```sh
chmod +x remove-tool.sh && ./remove-tool.sh
```

## Features

- Multi-tab terminal profiles for saving and reopening complete workspaces.
- PTY-backed tabbed terminal workspace.
- Cross-platform shell support for PowerShell, `cmd.exe`, and `bash`.
- Per-tab configuration for shell type, name, starting directory, startup script, font, and color scheme.
- Profile management for creating, renaming, deleting, saving, and opening profiles.
- Tab management for adding, editing, closing, renaming, and reordering tabs.
- Profile-wide and tab-level font settings.
- Global color scheme management with custom schemes and built-in defaults.
- Workspace-level profile saving.
- Local JSON-backed configuration under `~/.termrig/`.

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

You can manually edit these JSON files while Termrig is closed. If Termrig is open, save operations from the app may overwrite manual edits.

## Crash Logs

Termrig writes application and terminal tab crash logs under `~/.termrig/crashes/`.

Crash log filenames use this format:

```text
yyyyMMdd-HHmmss-{profilename}-{tabname}.log
```

To retrieve crash logs:

Windows PowerShell:

```powershell
Get-ChildItem "$env:USERPROFILE\.termrig\crashes"
```

Mac/Linux:

```sh
ls -la ~/.termrig/crashes/
```

Include the relevant `.log` file when filing a bug report.

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
