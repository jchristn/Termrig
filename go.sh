#!/usr/bin/env sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
cd "$SCRIPT_DIR"

cd src
dotnet build
cd ..
dotnet run --project src/Termrig.App/Termrig.App.csproj
