@echo off
rem Builds the Windows installers (Inno Setup .exe and portable .zip, x64 and arm64) into installers\<version>\.
rem Needs Inno Setup (choco install innosetup). Usage: build-installers.bat [version]
rem (default: the <Version> in src\Termrig.App\Termrig.App.csproj). CI runs the same channels; see .github\workflows\release.yml.
setlocal
cd /d "%~dp0"
dotnet build src\Termrig.Publisher -c Release --nologo -v quiet || exit /b 1
if "%~1"=="" (
  dotnet run --project src\Termrig.Publisher -c Release --no-build -- --all
) else (
  dotnet run --project src\Termrig.Publisher -c Release --no-build -- --all --version %1
)
exit /b %ERRORLEVEL%
