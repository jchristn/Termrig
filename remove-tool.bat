@echo off
setlocal

dotnet tool uninstall --global Termrig >nul 2>nul
if errorlevel 1 (
    echo Termrig global tool is not installed.
    exit /b 0
)

echo Removed Termrig global tool.
