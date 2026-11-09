@echo off
rem Windows entry point; the root script reinstalls the trig command and the Start menu app.
setlocal
if not "%OS%"=="Windows_NT" (
    echo This script requires Windows.
    exit /b 1
)
call "%~dp0..\..\reinstall-tool.bat" %*
exit /b %errorlevel%
