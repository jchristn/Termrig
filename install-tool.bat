@echo off
setlocal

set SCRIPT_DIR=%~dp0
set PROJECT=%SCRIPT_DIR%src\Termrig.App\Termrig.App.csproj
set PACKAGE_DIR=%SCRIPT_DIR%artifacts\tools
set NUGET_PACKAGE_DIR=%USERPROFILE%\.nuget\packages\termrig\0.1.0

if exist "%PACKAGE_DIR%" rmdir /s /q "%PACKAGE_DIR%"
mkdir "%PACKAGE_DIR%"
if exist "%NUGET_PACKAGE_DIR%" rmdir /s /q "%NUGET_PACKAGE_DIR%"

dotnet build "%PROJECT%" --configuration Release
if errorlevel 1 exit /b %errorlevel%

dotnet pack "%PROJECT%" --configuration Release --no-build --output "%PACKAGE_DIR%"
if errorlevel 1 exit /b %errorlevel%

dotnet tool list --global | findstr /I /R "^termrig " >nul
if not errorlevel 1 (
    dotnet tool uninstall --global Termrig
    if errorlevel 1 (
        echo Failed to uninstall existing Termrig tool. Close any running Termrig/tr processes and rerun this script.
        exit /b %errorlevel%
    )
)

dotnet tool install --global Termrig --version 0.1.0 --source "%PACKAGE_DIR%" --no-http-cache
if errorlevel 1 exit /b %errorlevel%

echo Installed Termrig as global command: tr
