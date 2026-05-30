[CmdletBinding()]
param(
    [ValidateSet('clean', 'restore', 'test', 'publish', 'package-windows', 'package-macos', 'package-ubuntu', 'package-all')]
    [string] $Target = 'package-all',

    [ValidateSet('win-x64', 'osx-x64', 'osx-arm64', 'linux-x64', 'all')]
    [string] $Runtime = 'all'
)

$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RepoRoot 'src/Termrig.App/Termrig.App.csproj'
$Solution = Join-Path $RepoRoot 'src/Termrig.slnx'
$Artifacts = Join-Path $RepoRoot 'artifacts'
$PublishRoot = Join-Path $Artifacts 'publish'
$PackageRoot = Join-Path $Artifacts 'packages'
$LogRoot = Join-Path $Artifacts 'logs'
$PackagingRoot = Join-Path $RepoRoot 'packaging'
$AppName = 'Termrig'
$AppId = 'com.jchristn.Termrig'
$PackageName = 'termrig'
$RunId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), $PID

function New-Directory {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Invoke-Logged {
    param(
        [string] $Name,
        [string] $FilePath,
        [string[]] $Arguments
    )

    New-Directory $LogRoot
    $LogFile = Join-Path $LogRoot "$Name-$RunId.log"
    Write-Host "==> $FilePath $($Arguments -join ' ')"
    & $FilePath @Arguments 2>&1 | Tee-Object -FilePath $LogFile
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE. See $LogFile"
    }
}

function Get-Version {
    [xml] $ProjectXml = Get-Content -LiteralPath $Project
    $Version = $ProjectXml.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($Version)) {
        throw "Version not found in $Project"
    }

    return $Version
}

function Get-Runtimes {
    if ($Runtime -eq 'all') {
        return @('win-x64', 'osx-x64', 'osx-arm64', 'linux-x64')
    }

    return @($Runtime)
}

function Invoke-Clean {
    if (Test-Path -LiteralPath $Artifacts) {
        Remove-Item -LiteralPath $Artifacts -Recurse -Force
    }
    New-Directory $PublishRoot
    New-Directory $PackageRoot
    New-Directory $LogRoot
}

function Invoke-Restore {
    New-Directory $LogRoot
    Invoke-Logged -Name 'restore' -FilePath 'dotnet' -Arguments @('restore', $Solution)
}

function Invoke-Tests {
    Invoke-Logged -Name 'build-release' -FilePath 'dotnet' -Arguments @('build', $Solution, '--configuration', 'Release', '--no-restore')
    Invoke-Logged -Name 'test-automated' -FilePath 'dotnet' -Arguments @('run', '--project', (Join-Path $RepoRoot 'src/Test.Automated/Test.Automated.csproj'), '--configuration', 'Release', '--framework', 'net10.0', '--no-restore')
    Invoke-Logged -Name 'test-xunit' -FilePath 'dotnet' -Arguments @('test', (Join-Path $RepoRoot 'src/Test.Xunit/Test.Xunit.csproj'), '--configuration', 'Release', '--no-restore')
    Invoke-Logged -Name 'test-nunit' -FilePath 'dotnet' -Arguments @('test', (Join-Path $RepoRoot 'src/Test.Nunit/Test.Nunit.csproj'), '--configuration', 'Release', '--no-restore')
}

function Invoke-Publish {
    New-Directory $PublishRoot
    foreach ($Rid in Get-Runtimes) {
        $Output = Join-Path $PublishRoot $Rid
        Invoke-Logged -Name "publish-$Rid" -FilePath 'dotnet' -Arguments @(
            'publish',
            $Project,
            '--configuration',
            'Release',
            '--runtime',
            $Rid,
            '--self-contained',
            'true',
            '--output',
            $Output
        )
    }
}

function Copy-License {
    param([string] $Destination)
    Copy-Item -LiteralPath (Join-Path $RepoRoot 'LICENSE.md') -Destination $Destination -Force
}

function Invoke-PackageWindows {
    $Version = Get-Version
    $PublishPath = Join-Path $PublishRoot 'win-x64'
    if (-not (Test-Path -LiteralPath $PublishPath)) {
        $script:Runtime = 'win-x64'
        Invoke-Publish
    }

    New-Directory $PackageRoot
    $ZipPath = Join-Path $PackageRoot "termrig-$Version-windows-x64-portable.zip"
    if (Test-Path -LiteralPath $ZipPath) {
        Remove-Item -LiteralPath $ZipPath -Force
    }
    Compress-Archive -Path (Join-Path $PublishPath '*') -DestinationPath $ZipPath -Force

    $Iscc = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($Iscc) {
        $InstallerOutput = Join-Path $PackageRoot 'windows'
        New-Directory $InstallerOutput
        Invoke-Logged -Name 'package-windows-installer' -FilePath $Iscc.Source -Arguments @(
            "/DAppVersion=$Version",
            "/DPublishDir=$PublishPath",
            "/DOutputDir=$InstallerOutput",
            (Join-Path $PackagingRoot 'windows/Termrig.iss')
        )
    } else {
        Write-Host 'Inno Setup ISCC.exe not found; skipped Windows installer and produced portable zip only.'
    }

    Write-Host "Windows artifacts:"
    Write-Host "  $ZipPath"
}

function Invoke-PackageMacos {
    $Version = Get-Version
    New-Directory $PackageRoot
    foreach ($Rid in @('osx-x64', 'osx-arm64')) {
        $PublishPath = Join-Path $PublishRoot $Rid
        if (-not (Test-Path -LiteralPath $PublishPath)) {
            $script:Runtime = $Rid
            Invoke-Publish
        }

        $StageRoot = Join-Path $PackageRoot "macos-$Rid"
        $AppRoot = Join-Path $StageRoot "$AppName.app"
        $Contents = Join-Path $AppRoot 'Contents'
        $MacOS = Join-Path $Contents 'MacOS'
        $Resources = Join-Path $Contents 'Resources'
        if (Test-Path -LiteralPath $StageRoot) {
            Remove-Item -LiteralPath $StageRoot -Recurse -Force
        }
        New-Directory $MacOS
        New-Directory $Resources

        Copy-Item -Path (Join-Path $PublishPath '*') -Destination $MacOS -Recurse -Force
        Copy-Item -LiteralPath (Join-Path $PackagingRoot 'macos/Info.plist') -Destination (Join-Path $Contents 'Info.plist') -Force
        $Icns = Join-Path $PackagingRoot 'icons/Termrig.icns'
        if (Test-Path -LiteralPath $Icns) {
            Copy-Item -LiteralPath $Icns -Destination (Join-Path $Resources 'Termrig.icns') -Force
        } else {
            Copy-Item -LiteralPath (Join-Path $PackagingRoot 'icons/hicolor/scalable/apps/com.jchristn.Termrig.svg') -Destination (Join-Path $Resources 'Termrig.svg') -Force
            Write-Host 'Termrig.icns not found; copied SVG icon into bundle resources. Generate .icns on macOS before public release.'
        }
        Copy-License -Destination $StageRoot

        if ($IsMacOS) {
            Invoke-Logged -Name "chmod-macos-$Rid" -FilePath 'chmod' -Arguments @('+x', (Join-Path $MacOS $AppName))
            $DmgPath = Join-Path $PackageRoot "termrig-$Version-$Rid.dmg"
            if (Test-Path -LiteralPath $DmgPath) {
                Remove-Item -LiteralPath $DmgPath -Force
            }
            Invoke-Logged -Name "package-macos-$Rid" -FilePath 'hdiutil' -Arguments @('create', '-volname', 'Termrig', '-srcfolder', $StageRoot, '-ov', '-format', 'UDZO', $DmgPath)
            Write-Host "macOS artifact: $DmgPath"
        } else {
            $ZipPath = Join-Path $PackageRoot "termrig-$Version-$Rid-app.zip"
            if (Test-Path -LiteralPath $ZipPath) {
                Remove-Item -LiteralPath $ZipPath -Force
            }
            Compress-Archive -Path (Join-Path $StageRoot '*') -DestinationPath $ZipPath -Force
            Write-Host "macOS app bundle staged as zip: $ZipPath"
        }
    }
}

function Invoke-PackageUbuntu {
    $Version = Get-Version
    $PublishPath = Join-Path $PublishRoot 'linux-x64'
    if (-not (Test-Path -LiteralPath $PublishPath)) {
        $script:Runtime = 'linux-x64'
        Invoke-Publish
    }

    $DebRoot = Join-Path $PackageRoot 'deb/termrig'
    if (Test-Path -LiteralPath $DebRoot) {
        Remove-Item -LiteralPath $DebRoot -Recurse -Force
    }

    $OptRoot = Join-Path $DebRoot 'opt/termrig'
    $DesktopRoot = Join-Path $DebRoot 'usr/share/applications'
    $IconRoot = Join-Path $DebRoot 'usr/share/icons/hicolor/scalable/apps'
    $DocRoot = Join-Path $DebRoot 'usr/share/doc/termrig'
    $DebianRoot = Join-Path $DebRoot 'DEBIAN'
    New-Directory $OptRoot
    New-Directory $DesktopRoot
    New-Directory $IconRoot
    New-Directory $DocRoot
    New-Directory $DebianRoot

    Copy-Item -Path (Join-Path $PublishPath '*') -Destination $OptRoot -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $PackagingRoot 'linux/com.jchristn.Termrig.desktop') -Destination $DesktopRoot -Force
    Copy-Item -LiteralPath (Join-Path $PackagingRoot 'icons/hicolor/scalable/apps/com.jchristn.Termrig.svg') -Destination $IconRoot -Force
    Copy-License -Destination $DocRoot

    $Control = Get-Content -LiteralPath (Join-Path $PackagingRoot 'linux/debian/control') -Raw
    $Control = $Control.Replace('@VERSION@', $Version)
    Set-Content -LiteralPath (Join-Path $DebianRoot 'control') -Value $Control -NoNewline
    Copy-Item -LiteralPath (Join-Path $PackagingRoot 'linux/debian/postinst') -Destination $DebianRoot -Force
    Copy-Item -LiteralPath (Join-Path $PackagingRoot 'linux/debian/postrm') -Destination $DebianRoot -Force

    if ($IsLinux) {
        Invoke-Logged -Name 'chmod-linux-package' -FilePath 'chmod' -Arguments @('755', (Join-Path $OptRoot 'Termrig'), (Join-Path $DebianRoot 'postinst'), (Join-Path $DebianRoot 'postrm'))
        $Validator = Get-Command 'desktop-file-validate' -ErrorAction SilentlyContinue
        if ($Validator) {
            Invoke-Logged -Name 'desktop-file-validate' -FilePath $Validator.Source -Arguments @((Join-Path $DesktopRoot 'com.jchristn.Termrig.desktop'))
        }

        $DpkgDeb = Get-Command 'dpkg-deb' -ErrorAction SilentlyContinue
        if (-not $DpkgDeb) {
            throw 'dpkg-deb not found; cannot build Ubuntu package on this host.'
        }

        $DebPath = Join-Path $PackageRoot "termrig_${Version}_amd64.deb"
        Invoke-Logged -Name 'package-ubuntu-deb' -FilePath $DpkgDeb.Source -Arguments @('--build', $DebRoot, $DebPath)
        Write-Host "Ubuntu artifact: $DebPath"
    } else {
        Write-Host "Ubuntu package staging created at $DebRoot. Run this target on Linux to build the .deb."
    }

    $TarPath = Join-Path $PackageRoot "termrig-$Version-linux-x64.tar.gz"
    if ($IsLinux -or $IsMacOS) {
        Invoke-Logged -Name 'package-linux-tar' -FilePath 'tar' -Arguments @('-czf', $TarPath, '-C', $PublishPath, '.')
        Write-Host "Linux portable archive: $TarPath"
    }
}

switch ($Target) {
    'clean' { Invoke-Clean }
    'restore' { Invoke-Restore }
    'test' { Invoke-Restore; Invoke-Tests }
    'publish' { Invoke-Restore; Invoke-Publish }
    'package-windows' { Invoke-Restore; Invoke-PackageWindows }
    'package-macos' { Invoke-Restore; Invoke-PackageMacos }
    'package-ubuntu' { Invoke-Restore; Invoke-PackageUbuntu }
    'package-all' {
        Invoke-Restore
        Invoke-Tests
        Invoke-Publish
        Invoke-PackageWindows
        Invoke-PackageMacos
        Invoke-PackageUbuntu
    }
}
