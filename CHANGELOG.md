# Changelog

## 0.1.1

- Updated NuGet dependencies:
  - Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter, Avalonia.Controls.ColorPicker 12.0.2 -> 12.1.3
  - CommunityToolkit.Mvvm 8.2.1 -> 8.4.2
  - Porta.Pty 1.0.7 -> 2.2.3
  - Wcwidth 3.0.0 -> 4.0.1 (XTerm.NET)
  - Touchstone.Core / Cli / XunitAdapter / NunitAdapter 0.1.12 -> 0.2.0
  - Microsoft.NET.Test.Sdk 17.14.1 -> 18.10.1
  - xunit.runner.visualstudio 3.1.4 -> 4.0.0
  - NUnit 4.3.2 -> 5.0.0, NUnit.Analyzers 4.7.0 -> 4.15.0, NUnit3TestAdapter 5.0.0 -> 6.3.0
  - coverlet.collector 6.0.4 -> 10.1.0
- Added dependency compatibility tests covering Unicode cell-width calculation and PTY process spawning.

## 0.1.0

- Initial Avalonia MVP.
- Added profile persistence under `~/.termrig`.
- Added shell detection and launch-plan support for `cmd.exe`, PowerShell, and bash.
- Added tabbed PTY terminal workspace and profile save flow.
