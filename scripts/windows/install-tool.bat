@echo off
rem Windows entry point; the root script installs the trig command and the Start menu app.
setlocal
if not "%OS%"=="Windows_NT" (
    echo This script requires Windows.
    exit /b 1
)
call "%~dp0..\..\install-tool.bat" %*
exit /b %errorlevel%
