@echo off
setlocal

REM Check first, so a failed uninstall (for example while trig.exe is running) is reported as a
REM failure instead of "not installed".
dotnet tool list --global | findstr /R /I /B /C:"termrig " >nul
if errorlevel 1 goto :tool_absent
dotnet tool uninstall --global Termrig >nul
if errorlevel 1 (
    echo Failed to uninstall the Termrig global tool. Close any running Termrig/trig processes and rerun this script.
    exit /b 1
)
echo Removed Termrig global tool.
goto :remove_app
:tool_absent
echo Termrig global tool is not installed.
:remove_app

set "APP_DIR=%LOCALAPPDATA%\Programs\Termrig"
REM The echo stays outside a ( ) block: a path such as C:\Users\Bob (Work) would end the block early.
if not exist "%APP_DIR%" goto :remove_links
rmdir /s /q "%APP_DIR%"
echo Removed %APP_DIR%.
:remove_links

set "START_MENU_LINK=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Termrig.lnk"
if exist "%START_MENU_LINK%" (
    del /q "%START_MENU_LINK%"
    echo Removed the Start menu shortcut.
)

REM build-app.ps1 -DesktopShortcut writes to the shell Desktop folder, which OneDrive can redirect.
set "DESKTOP_DIR="
for /f "usebackq delims=" %%d in (`powershell -NoProfile -Command "[Environment]::GetFolderPath('Desktop')"`) do set "DESKTOP_DIR=%%d"
if not defined DESKTOP_DIR set "DESKTOP_DIR=%USERPROFILE%\Desktop"
set "DESKTOP_LINK=%DESKTOP_DIR%\Termrig.lnk"
if exist "%DESKTOP_LINK%" (
    del /q "%DESKTOP_LINK%"
    echo Removed the desktop shortcut.
)

exit /b 0
