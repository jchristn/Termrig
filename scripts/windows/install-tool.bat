@echo off
REM Build and install Termrig from this checkout: the trig .NET global tool and the
REM self-contained desktop app. Fails if either is already installed; use reinstall-tool.bat
REM to replace an existing install.
REM On Windows the app is %LOCALAPPDATA%\Programs\Termrig, with a Start menu shortcut
REM (Termrig.lnk in the Start menu Programs folder) so it can be pinned to the taskbar.
REM Refuses while Termrig is running. The tool package is built under artifacts\tools.
REM Usage: scripts\windows\install-tool.bat  (-h or --help shows this; no other arguments)
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

for %%i in ("%~dp0..\..") do set "ROOT_DIR=%%~fi"
set "PROJECT=%ROOT_DIR%\src\Termrig.App\Termrig.App.csproj"
set "VERSION="
for /f "tokens=2 delims=<>	 " %%v in ('findstr /L /C:"<Version>" "%PROJECT%"') do if not defined VERSION set "VERSION=%%v"
if not defined VERSION (
    echo Error: Cannot read the Version from src\Termrig.App\Termrig.App.csproj. 1>&2
    goto :fail
)
set "PACKAGE_SOURCE=%ROOT_DIR%\artifacts\tools"
REM dotnet tool install reuses a cached package with the same version, so the cached copy is dropped.
set "NUGET_ROOT=%USERPROFILE%\.nuget\packages"
if defined NUGET_PACKAGES set "NUGET_ROOT=%NUGET_PACKAGES%"
set "NUGET_CACHE=%NUGET_ROOT%\termrig\%VERSION%"

set "ARCH=%PROCESSOR_ARCHITECTURE%"
if defined PROCESSOR_ARCHITEW6432 set "ARCH=%PROCESSOR_ARCHITEW6432%"
set "RID="
if /i "%ARCH%"=="AMD64" set "RID=win-x64"
if /i "%ARCH%"=="ARM64" set "RID=win-arm64"
if not defined RID (
    echo Error: Unsupported architecture: %ARCH%. Termrig builds for x64 and ARM64. 1>&2
    goto :fail
)
set "STAGE_DIR=%ROOT_DIR%\artifacts\windows-app\%RID%"
set "PUBLISH_DIR=%STAGE_DIR%\publish"

REM The app and its Start menu shortcut (the same paths as packaging\windows\build-app.ps1).
set "APP_DIR=%LOCALAPPDATA%\Programs\Termrig"
set "APP_EXE=%APP_DIR%\Termrig.exe"
call :folder Programs "%APPDATA%\Microsoft\Windows\Start Menu\Programs"
set "START_MENU_LINK=%FOLDER%\Termrig.lnk"

echo Termrig install (Windows, %VERSION%, %RID%)
echo [1/7] Checking for running Termrig processes...
call :termrig_running
if not errorlevel 1 goto :running
echo [2/7] Checking for an existing install...
call :tool_installed
if not errorlevel 1 (
    echo Error: The Termrig global tool ^(trig^) is already installed. Run scripts\windows\reinstall-tool.bat to replace it. 1>&2
    goto :fail
)
set "APP_INSTALLED=0"
if exist "%APP_DIR%\" set "APP_INSTALLED=1"
if exist "%START_MENU_LINK%" set "APP_INSTALLED=1"
if "%APP_INSTALLED%"=="1" (
    echo Error: The Termrig app is already installed. Run scripts\windows\reinstall-tool.bat to replace it. 1>&2
    goto :fail
)
echo [3/7] Building the Termrig tool package...
if exist "%PACKAGE_SOURCE%" rmdir /s /q "%PACKAGE_SOURCE%"
if exist "%NUGET_CACHE%" rmdir /s /q "%NUGET_CACHE%"
if exist "%PACKAGE_SOURCE%" goto :cleanup_failed
if exist "%NUGET_CACHE%" goto :cleanup_failed
mkdir "%PACKAGE_SOURCE%"
if errorlevel 1 (
    echo Error: Could not create the package directory under artifacts. 1>&2
    goto :fail
)
dotnet pack "%PROJECT%" --configuration Release --output "%PACKAGE_SOURCE%"
if errorlevel 1 (
    echo Error: dotnet pack failed. 1>&2
    goto :fail
)
echo [4/7] Publishing the Termrig app ^(%RID%^)...
call :publish_app
if errorlevel 1 goto :fail
echo [5/7] Installing the Termrig global tool...
dotnet tool install -g Termrig --version %VERSION% --source "%PACKAGE_SOURCE%" --no-http-cache
if errorlevel 1 (
    echo Error: dotnet tool install failed. 1>&2
    goto :fail
)
echo [6/7] Installing the Termrig app...
call :install_app
if errorlevel 1 goto :fail
echo [7/7] Verifying the install...
call :tool_installed
if errorlevel 1 (
    echo Error: Termrig is not listed by "dotnet tool list -g" after the install. 1>&2
    goto :fail
)
REM trig opens the app, so it is located rather than run. dotnet tool install puts it in
REM %USERPROFILE%\.dotnet\tools, which may not be on this session's PATH yet.
set "TRIG_FOUND=0"
where trig >nul 2>nul && set "TRIG_FOUND=1"
if exist "%USERPROFILE%\.dotnet\tools\trig.exe" set "TRIG_FOUND=1"
if "%TRIG_FOUND%"=="0" (
    echo Error: Termrig was installed, but trig is not on PATH or in %%USERPROFILE%%\.dotnet\tools. 1>&2
    goto :fail
)
if not exist "%APP_EXE%" (
    echo Error: The Termrig app was not installed. 1>&2
    goto :fail
)
where trig >nul 2>nul || echo Note: open a new terminal so %%USERPROFILE%%\.dotnet\tools is on PATH and 'trig' is found.
echo Done. Start Termrig with 'trig' or from the Start menu.
endlocal & exit /b 0

:usage
echo Usage: scripts\windows\install-tool.bat
echo Builds and installs the trig global tool and the Termrig app. Fails if Termrig is already installed; use reinstall-tool.bat.
endlocal & exit /b 0

REM kill-termrig.bat would close this very tab when run from inside Termrig, so point elsewhere.
:running
if /i "%TERM_PROGRAM%"=="Termrig" goto :running_here
echo Error: Termrig is running. Quit every Termrig window and trig session (or run scripts\windows\kill-termrig.bat) and try again. 1>&2
goto :fail
:running_here
echo Error: Termrig is running, and this terminal is a Termrig tab. Quit Termrig and run this from another terminal. 1>&2
goto :fail

:cleanup_failed
echo Error: Could not delete the previous tool package or its NuGet cache; close anything using them and try again. 1>&2
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

REM Publishes the self-contained app into the staging directory; nothing installed changes.
:publish_app
if exist "%STAGE_DIR%" rmdir /s /q "%STAGE_DIR%"
if exist "%STAGE_DIR%" (
    echo Error: Could not delete the previous build under artifacts\windows-app; close anything using it and try again. 1>&2
    exit /b 1
)
dotnet publish "%PROJECT%" --configuration Release --runtime %RID% --self-contained true --output "%PUBLISH_DIR%" -p:PackAsTool=false
if errorlevel 1 (
    echo Error: dotnet publish failed. 1>&2
    exit /b 1
)
if not exist "%PUBLISH_DIR%\Termrig.exe" (
    echo Error: dotnet publish did not produce Termrig.exe. 1>&2
    exit /b 1
)
exit /b 0

REM Replaces the installed app with the published one and writes the Start menu shortcut.
:install_app
if exist "%APP_DIR%" rmdir /s /q "%APP_DIR%"
if exist "%APP_DIR%" (
    echo Error: Could not replace %%LOCALAPPDATA%%\Programs\Termrig; close anything using it and try again. 1>&2
    exit /b 1
)
xcopy "%PUBLISH_DIR%" "%APP_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 (
    echo Error: Could not copy the app to %%LOCALAPPDATA%%\Programs\Termrig. 1>&2
    exit /b 1
)
set "TERMRIG_APP_EXE=%APP_EXE%"
set "TERMRIG_APP_DIR=%APP_DIR%"
set "TERMRIG_SHORTCUT=%START_MENU_LINK%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ErrorActionPreference = 'Stop'; $s = (New-Object -ComObject WScript.Shell).CreateShortcut($env:TERMRIG_SHORTCUT); $s.TargetPath = $env:TERMRIG_APP_EXE; $s.WorkingDirectory = $env:TERMRIG_APP_DIR; $s.IconLocation = $env:TERMRIG_APP_EXE + ',0'; $s.Description = 'Termrig terminal profiles and workspaces'; $s.Save()"
if errorlevel 1 (
    echo Error: Could not create the Start menu shortcut. 1>&2
    exit /b 1
)
echo Installed %APP_DIR% and %START_MENU_LINK%
exit /b 0
