@echo off
rem Windows entry point; the root script removes the trig command and the Start menu app.
setlocal
if not "%OS%"=="Windows_NT" (
    echo This script requires Windows.
    exit /b 1
)
call "%~dp0..\..\remove-tool.bat" %*
exit /b %errorlevel%
