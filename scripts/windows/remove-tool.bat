@echo off
REM Remove everything install-tool.bat installs: the trig .NET global tool and the desktop app.
REM Succeeds with a message when nothing is installed. Refuses while Termrig is running.
REM On Windows it deletes %LOCALAPPDATA%\Programs\Termrig, the Start menu shortcut and the
REM desktop shortcut that packaging\windows\build-app.ps1 -DesktopShortcut creates.
REM Your profiles and settings in %USERPROFILE%\.termrig are kept.
REM Usage: scripts\windows\remove-tool.bat  (-h or --help shows this; no other arguments)
setlocal
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
if "%~1"=="/?" goto :usage
if not "%~1"=="" goto :bad_argument

where dotnet >nul 2>nul
if errorlevel 1 (
    echo Error: dotnet was not found on PATH. Install the .NET 10 SDK: https://dotnet.microsoft.com/download 1>&2
    goto :fail
)
where powershell >nul 2>nul
if errorlevel 1 (
    echo Error: powershell was not found on PATH. Windows PowerShell ships with Windows; add %%SystemRoot%%\System32\WindowsPowerShell\v1.0 to PATH. 1>&2
    goto :fail
)

REM The app and its shortcuts (the same paths as packaging\windows\build-app.ps1).
set "APP_DIR=%LOCALAPPDATA%\Programs\Termrig"
call :folder Programs "%APPDATA%\Microsoft\Windows\Start Menu\Programs"
set "START_MENU_LINK=%FOLDER%\Termrig.lnk"
call :folder Desktop "%USERPROFILE%\Desktop"
set "DESKTOP_LINK=%FOLDER%\Termrig.lnk"

echo Termrig removal (Windows)
echo [1/3] Checking for running Termrig processes...
call :termrig_running
if not errorlevel 1 goto :running
set "REMOVED=0"
echo [2/3] Removing the Termrig global tool...
call :tool_installed
if errorlevel 1 goto :tool_absent
dotnet tool uninstall -g Termrig
if errorlevel 1 (
    echo Error: Failed to uninstall the Termrig global tool. 1>&2
    goto :fail
)
set "REMOVED=1"
goto :remove_app
:tool_absent
echo The Termrig global tool is not installed.

:remove_app
echo [3/3] Removing the Termrig app...
set "APP_FOUND=0"
if exist "%APP_DIR%\" set "APP_FOUND=1"
if exist "%START_MENU_LINK%" set "APP_FOUND=1"
if exist "%DESKTOP_LINK%" set "APP_FOUND=1"
if "%APP_FOUND%"=="0" goto :app_absent
if not exist "%APP_DIR%\" goto :remove_links
rmdir /s /q "%APP_DIR%"
if exist "%APP_DIR%\" (
    echo Error: Could not delete %%LOCALAPPDATA%%\Programs\Termrig; close anything using it and try again. 1>&2
    goto :fail
)
echo Removed %APP_DIR%
:remove_links
call :remove_file "%START_MENU_LINK%"
if errorlevel 1 goto :fail
call :remove_file "%DESKTOP_LINK%"
if errorlevel 1 goto :fail
set "REMOVED=1"
goto :summary
:app_absent
echo The Termrig app is not installed.

:summary
if "%REMOVED%"=="0" echo Termrig is not installed; nothing to remove.
echo Done. Your profiles and settings in %%USERPROFILE%%\.termrig were kept.
endlocal & exit /b 0

:usage
echo Usage: scripts\windows\remove-tool.bat
echo Removes the trig global tool and the Termrig app. Succeeds when nothing is installed.
endlocal & exit /b 0

REM kill-termrig.bat would close this very tab when run from inside Termrig, so point elsewhere.
:running
if /i "%TERM_PROGRAM%"=="Termrig" goto :running_here
echo Error: Termrig is running. Quit every Termrig window and trig session (or run scripts\windows\kill-termrig.bat) and try again. 1>&2
goto :fail
:running_here
echo Error: Termrig is running, and this terminal is a Termrig tab. Quit Termrig and run this from another terminal. 1>&2
goto :fail

:bad_argument
echo Error: Unexpected argument: %1 (this script takes no arguments) 1>&2
goto :fail

:fail
endlocal & exit /b 1

REM Sets FOLDER to a shell folder ([Environment]::GetFolderPath), or to the fallback in %2.
:folder
set "FOLDER="
for /f "usebackq delims=" %%d in (`powershell -NoProfile -Command "[Environment]::GetFolderPath('%~1')"`) do set "FOLDER=%%d"
if not defined FOLDER set "FOLDER=%~2"
exit /b 0

REM Exits 0 when this user runs trig.exe (the global tool), Termrig.exe (the installed app, or a
REM dotnet run build) or "dotnet Termrig.dll". Other users' processes do not count.
:termrig_running
tasklist /FI "IMAGENAME eq trig.exe" /FI "USERNAME eq %USERDOMAIN%\%USERNAME%" 2>nul | find /I "trig.exe" >nul
if not errorlevel 1 exit /b 0
tasklist /FI "IMAGENAME eq Termrig.exe" /FI "USERNAME eq %USERDOMAIN%\%USERNAME%" 2>nul | find /I "Termrig.exe" >nul
if not errorlevel 1 exit /b 0
powershell -NoProfile -ExecutionPolicy Bypass -Command "$p = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -ieq 'dotnet.exe' -and $_.CommandLine -match 'Termrig\.dll' -and (Invoke-CimMethod -InputObject $_ -MethodName GetOwner).User -eq $env:USERNAME }); if ($p.Count -gt 0) { exit 0 } else { exit 1 }"
if not errorlevel 1 exit /b 0
exit /b 1

REM Exits 0 when the Termrig global tool (the trig command) is installed.
:tool_installed
dotnet tool list -g 2>nul | findstr /R /I /B /C:"termrig " >nul
exit /b %errorlevel%

REM Deletes the file in %1 when it exists. Prints what it removed.
:remove_file
if not exist "%~1" exit /b 0
del /q "%~1"
if exist "%~1" (
    echo Error: Could not delete a Termrig shortcut; close anything using it and try again. 1>&2
    exit /b 1
)
echo Removed %~1
exit /b 0
