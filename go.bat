@echo off
cd /d "%~dp0src" || exit /b 1
dotnet build
if errorlevel 1 exit /b 1
cd ..
dotnet run --project src\Termrig.App\Termrig.App.csproj
