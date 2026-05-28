@echo off
setlocal

set SCRIPT_DIR=%~dp0
set PROJECT=%SCRIPT_DIR%src\Termrig.App\Termrig.App.csproj
set PACKAGE_DIR=%SCRIPT_DIR%artifacts\tools

dotnet tool uninstall --global Termrig >nul 2>nul

dotnet build "%PROJECT%" --configuration Release
if errorlevel 1 exit /b %errorlevel%

dotnet pack "%PROJECT%" --configuration Release --no-build --output "%PACKAGE_DIR%"
if errorlevel 1 exit /b %errorlevel%

dotnet tool install --global Termrig --version 0.1.0 --add-source "%PACKAGE_DIR%"
if errorlevel 1 exit /b %errorlevel%

echo Installed Termrig as global command: tr
