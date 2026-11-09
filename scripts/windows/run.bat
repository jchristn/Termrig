@echo off
rem Refresh the self-contained app in %LOCALAPPDATA%\Programs\Termrig, then launch that installed copy.
setlocal
if not "%OS%"=="Windows_NT" (
    echo This script requires Windows.
    exit /b 1
)
if not "%~1"=="" (
    echo Usage: %~nx0
    exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\packaging\windows\build-app.ps1"
if errorlevel 1 exit /b 1
set "APP_EXE=%LOCALAPPDATA%\Programs\Termrig\Termrig.exe"
if not exist "%APP_EXE%" (
    echo Termrig was not installed at %APP_EXE%.
    exit /b 1
)
start "" "%APP_EXE%"
exit /b 0
