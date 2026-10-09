@echo off
setlocal

dotnet tool uninstall --global Termrig >nul 2>nul
if errorlevel 1 (
    echo Termrig global tool is not installed.
) else (
    echo Removed Termrig global tool.
)

set APP_DIR=%LOCALAPPDATA%\Programs\Termrig
if exist "%APP_DIR%" (
    rmdir /s /q "%APP_DIR%"
    echo Removed %APP_DIR%.
)

set START_MENU_LINK=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Termrig.lnk
if exist "%START_MENU_LINK%" (
    del /q "%START_MENU_LINK%"
    echo Removed the Start menu shortcut.
)

set DESKTOP_LINK=%USERPROFILE%\Desktop\Termrig.lnk
if exist "%DESKTOP_LINK%" (
    del /q "%DESKTOP_LINK%"
    echo Removed the desktop shortcut.
)

exit /b 0
