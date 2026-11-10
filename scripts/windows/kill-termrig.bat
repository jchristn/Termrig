@echo off
REM Stop every running Termrig process of this user: the installed app (Termrig.exe, also a dotnet
REM run build), the trig global tool (trig.exe) and copies started with "dotnet Termrig.dll", with
REM their child processes (taskkill /T). Asks them to close, waits up to 5 seconds, then
REM force-kills what is left. Succeeds when nothing is running; fails if something could not be
REM stopped. Every terminal tab inside Termrig closes with it, including this one if you run it
REM from a Termrig tab. Other users' processes are left alone.
REM Usage: scripts\windows\kill-termrig.bat  (-h or --help shows this; no other arguments)
setlocal
if /i "%~1"=="-h" goto :usage
if /i "%~1"=="--help" goto :usage
if "%~1"=="/?" goto :usage
if not "%~1"=="" goto :bad_argument

where powershell >nul 2>nul
if errorlevel 1 (
    echo Error: powershell was not found on PATH. Windows PowerShell ships with Windows; add %%SystemRoot%%\System32\WindowsPowerShell\v1.0 to PATH. 1>&2
    goto :fail
)

echo Stopping Termrig processes...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "function Find-Termrig { Get-CimInstance Win32_Process | Where-Object { (($_.Name -ieq 'Termrig.exe') -or ($_.Name -ieq 'trig.exe') -or ($_.Name -ieq 'dotnet.exe' -and $_.CommandLine -match 'Termrig\.dll')) -and ((Invoke-CimMethod -InputObject $_ -MethodName GetOwner).User -eq $env:USERNAME) } };" ^
  "$p = @(Find-Termrig); if ($p.Count -eq 0) { Write-Output 'No Termrig processes are running.'; exit 0 };" ^
  "foreach ($x in $p) { & taskkill.exe /T /PID $x.ProcessId 2>$null | Out-Null };" ^
  "for ($i = 0; $i -lt 5 -and @(Find-Termrig).Count -gt 0; $i++) { Start-Sleep -Seconds 1 };" ^
  "$left = @(Find-Termrig); if ($left.Count -gt 0) { Write-Output 'Some Termrig processes did not exit; force-killing them.'; foreach ($x in $left) { & taskkill.exe /F /T /PID $x.ProcessId 2>$null | Out-Null }; Start-Sleep -Seconds 1 };" ^
  "$left = @(Find-Termrig); if ($left.Count -gt 0) { [Console]::Error.WriteLine('Error: Some Termrig processes could not be stopped: ' + (($left | ForEach-Object { $_.Name + ' (' + $_.ProcessId + ')' }) -join ', ')); exit 1 };" ^
  "Write-Output 'Done.'"
if errorlevel 1 goto :fail
endlocal & exit /b 0

:usage
echo Usage: scripts\windows\kill-termrig.bat
echo Stops every running Termrig process. Succeeds when nothing is running.
endlocal & exit /b 0

:bad_argument
echo Error: Unexpected argument: %1 (this script takes no arguments) 1>&2
goto :fail

:fail
endlocal & exit /b 1
