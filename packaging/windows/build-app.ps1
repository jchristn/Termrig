# Build Termrig for this machine and install it for the current user, with a Start
# menu shortcut so it can be pinned to the taskbar.
#
# Usage (from the repository root):
#   powershell -ExecutionPolicy Bypass -File packaging\windows\build-app.ps1 [-DesktopShortcut]
#
# Installs:
#   %LOCALAPPDATA%\Programs\Termrig\                      the app (self-contained)
#   Start menu\Programs\Termrig.lnk                       the shortcut
#   Desktop\Termrig.lnk                                   with -DesktopShortcut
#
# No administrator rights needed. Uninstall with remove-tool.bat, or delete those paths.
[CmdletBinding()]
param(
    [switch] $DesktopShortcut
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$Project = Join-Path $RepoRoot 'src\Termrig.App\Termrig.App.csproj'
$AppDir = Join-Path $env:LOCALAPPDATA 'Programs\Termrig'
$AppExe = Join-Path $AppDir 'Termrig.exe'
$StartMenuShortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Termrig.lnk'
$DesktopShortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Termrig.lnk'

if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { $Rid = 'win-arm64' } else { $Rid = 'win-x64' }

[xml] $ProjectXml = Get-Content -LiteralPath $Project
$Version = $ProjectXml.Project.PropertyGroup.Version | Select-Object -First 1
$StageDir = Join-Path $RepoRoot "artifacts\windows-app\$Rid"
$PublishDir = Join-Path $StageDir 'publish'

$Running = Get-Process -Name 'Termrig' -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -like "$AppDir\*" }
if ($Running) {
    throw "Termrig is running from $AppDir. Quit it and run this script again."
}

if (Test-Path -LiteralPath $StageDir) { Remove-Item -LiteralPath $StageDir -Recurse -Force }
New-Item -ItemType Directory -Path $StageDir | Out-Null

Write-Host "Publishing Termrig $Version ($Rid)..."
& dotnet publish $Project --configuration Release --runtime $Rid --self-contained true --output $PublishDir -p:PackAsTool=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

Write-Host "Installing to $AppDir..."
if (Test-Path -LiteralPath $AppDir) { Remove-Item -LiteralPath $AppDir -Recurse -Force }
New-Item -ItemType Directory -Path $AppDir | Out-Null
Copy-Item -Path (Join-Path $PublishDir '*') -Destination $AppDir -Recurse -Force

function New-Shortcut {
    param([string] $Path)
    $Shell = New-Object -ComObject WScript.Shell
    $Shortcut = $Shell.CreateShortcut($Path)
    $Shortcut.TargetPath = $AppExe
    $Shortcut.WorkingDirectory = $AppDir
    $Shortcut.IconLocation = "$AppExe,0"
    $Shortcut.Description = 'Termrig terminal profiles and workspaces'
    $Shortcut.Save()
}

Write-Host 'Creating the Start menu shortcut...'
New-Shortcut -Path $StartMenuShortcut
if ($DesktopShortcut) {
    Write-Host 'Creating the desktop shortcut...'
    New-Shortcut -Path $DesktopShortcutPath
}

Write-Host "Installed Termrig $Version. Open it from the Start menu (search for ""Termrig"")."
