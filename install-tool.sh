#!/usr/bin/env sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
PROJECT="$SCRIPT_DIR/src/Termrig.App/Termrig.App.csproj"
PACKAGE_DIR="$SCRIPT_DIR/artifacts/tools"
NUGET_PACKAGE_DIR="$HOME/.nuget/packages/termrig/0.1.0"

rm -rf "$PACKAGE_DIR"
mkdir -p "$PACKAGE_DIR"
rm -rf "$NUGET_PACKAGE_DIR"

dotnet build "$PROJECT" --configuration Release
dotnet pack "$PROJECT" --configuration Release --no-build --output "$PACKAGE_DIR"

if dotnet tool list --global | grep -qi '^termrig '; then
    dotnet tool uninstall --global Termrig || {
        echo "Failed to uninstall existing Termrig tool. Close any running Termrig/tr processes and rerun this script." >&2
        exit 1
    }
fi
dotnet tool install --global Termrig --version 0.1.0 --source "$PACKAGE_DIR" --no-http-cache

echo "Installed Termrig as global command: tr"
