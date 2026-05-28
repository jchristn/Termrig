#!/usr/bin/env sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
PROJECT="$SCRIPT_DIR/src/Termrig.App/Termrig.App.csproj"
PACKAGE_DIR="$SCRIPT_DIR/artifacts/tools"

dotnet tool uninstall --global Termrig >/dev/null 2>&1 || true

dotnet build "$PROJECT" --configuration Release
dotnet pack "$PROJECT" --configuration Release --no-build --output "$PACKAGE_DIR"
dotnet tool install --global Termrig --version 0.1.0 --add-source "$PACKAGE_DIR"

echo "Installed Termrig as global command: tr"
