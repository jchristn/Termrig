@echo off
setlocal

set "ROOT_DIR=%~dp0"
set "PROJECT=%ROOT_DIR%src\Termrig.App\Termrig.App.csproj"
set "PACKAGE_DIR=%ROOT_DIR%artifacts\tools"
set "NUGET_PACKAGE_DIR=%USERPROFILE%\.nuget\packages\termrig\0.1.0"

echo Termrig tool reinstall
echo Project: %PROJECT%
echo Package source: %PACKAGE_DIR%
echo.

echo [1/6] Checking for running trig.exe processes...
tasklist /FI "IMAGENAME eq trig.exe" 2>nul | find /I "trig.exe" >nul
if %errorlevel% equ 0 (
    echo A running trig.exe process is locking the global tool install.
    echo Exit all Termrig ^(trig^) sessions and rerun reinstall-tool.bat.
    exit /b 1
)
echo No running trig.exe process found.
echo.

echo [2/6] Removing existing Termrig global tool if present...
set "UNINSTALL_LOG=%TEMP%\termrig-tool-uninstall-%RANDOM%%RANDOM%.log"
dotnet tool uninstall --global Termrig > "%UNINSTALL_LOG%" 2>&1
if errorlevel 1 (
    findstr /I /C:"could not be found" /C:"not currently installed" /C:"is not installed" "%UNINSTALL_LOG%" >nul
    if errorlevel 1 (
        type "%UNINSTALL_LOG%"
        del "%UNINSTALL_LOG%" >nul 2>nul
        echo Failed to uninstall Termrig. Ensure no Termrig/trig processes are running and rerun this script.
        exit /b 1
    )
    echo Termrig is not installed; continuing...
) else (
    type "%UNINSTALL_LOG%"
)
del "%UNINSTALL_LOG%" >nul 2>nul
echo.

echo [3/6] Preparing package directory...
if exist "%PACKAGE_DIR%" rmdir /s /q "%PACKAGE_DIR%"
mkdir "%PACKAGE_DIR%"
if %errorlevel% neq 0 exit /b %errorlevel%
if exist "%NUGET_PACKAGE_DIR%" rmdir /s /q "%NUGET_PACKAGE_DIR%"
echo Package directory ready.
echo.

echo [4/6] Building Termrig...
dotnet build "%PROJECT%" --configuration Release
if %errorlevel% neq 0 (
    echo Build failed.
    exit /b %errorlevel%
)
echo Build complete.
echo.

echo [5/6] Packing and installing Termrig global tool...
dotnet pack "%PROJECT%" --configuration Release --no-build --output "%PACKAGE_DIR%"
if %errorlevel% neq 0 exit /b %errorlevel%
dotnet tool install --global Termrig --version 0.1.0 --source "%PACKAGE_DIR%" --no-http-cache
if %errorlevel% neq 0 exit /b %errorlevel%
echo Install complete.
echo.

echo [6/6] Verifying Termrig global tool...
dotnet tool list --global | findstr /I /R "^termrig " >nul
if %errorlevel% neq 0 (
    echo Verification failed: Termrig is not listed as a global tool.
    exit /b 1
)
echo Installed Termrig as global command: trig
exit /b 0
