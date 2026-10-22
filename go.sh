#!/usr/bin/env sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
cd "$SCRIPT_DIR"

if [ "$(uname -s)" = Darwin ]; then
    exec "$SCRIPT_DIR/scripts/macos/run.sh" "$@"
fi

cd src
dotnet build
cd ..
dotnet run --project src/Termrig.App/Termrig.App.csproj
