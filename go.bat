@echo off
cd src
dotnet build
cd ..
dotnet run --project src\Termrig.App\Termrig.App.csproj
