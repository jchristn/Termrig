#!/usr/bin/env sh
set -eu

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
PROJECT="$SCRIPT_DIR/src/Termrig.App/Termrig.App.csproj"
PACKAGE_DIR="$SCRIPT_DIR/artifacts/tools"
NUGET_PACKAGE_DIR="$HOME/.nuget/packages/termrig/0.1.1"

echo "Termrig tool reinstall"
echo "Project: $PROJECT"
echo "Package source: $PACKAGE_DIR"
echo

echo "[1/6] Removing existing Termrig global tool if present..."
dotnet tool uninstall --global Termrig 2>/dev/null || echo "Termrig is not installed; continuing..."
echo

echo "[2/6] Preparing package directory..."
rm -rf "$PACKAGE_DIR"
mkdir -p "$PACKAGE_DIR"
rm -rf "$NUGET_PACKAGE_DIR"
echo "Package directory ready."
echo

echo "[3/6] Building Termrig..."
dotnet build "$PROJECT" --configuration Release
echo

echo "[4/6] Packing and installing Termrig global tool..."
dotnet pack "$PROJECT" --configuration Release --no-build --output "$PACKAGE_DIR"
dotnet tool install --global Termrig --version 0.1.1 --source "$PACKAGE_DIR" --no-http-cache
echo

echo "[5/6] Verifying Termrig global tool..."
if ! dotnet tool list --global | grep -qi '^termrig '; then
    echo "Verification failed: Termrig is not listed as a global tool." >&2
    exit 1
fi

echo "Installed Termrig as global command: trig"
echo

echo "[6/6] Building Termrig.app..."
if [ "$(uname -s)" = "Darwin" ]; then
    "$SCRIPT_DIR/packaging/macos/build-app.sh"
else
    echo "Not macOS; skipping."
fi
