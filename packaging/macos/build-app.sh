#!/usr/bin/env sh
# Build Termrig.app for the host architecture and install it.
#
# Usage: packaging/macos/build-app.sh [install-dir]
#   install-dir  Where to place Termrig.app (default: $HOME/Applications).
#
# The bundle is self-contained, so it runs without the .NET runtime installed.
set -eu

if [ "$(uname -s)" != "Darwin" ]; then
    echo "build-app.sh: Termrig.app can only be built on macOS; skipping." >&2
    exit 0
fi

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/src/Termrig.App/Termrig.App.csproj"
ICON_SOURCE="$REPO_ROOT/packaging/icons/termrig-macos.png"
INSTALL_DIR="${1:-$HOME/Applications}"

case "$(uname -m)" in
    arm64) RID="osx-arm64" ;;
    *) RID="osx-x64" ;;
esac

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -n 1)"
STAGE_DIR="$REPO_ROOT/artifacts/macos-app/$RID"
PUBLISH_DIR="$STAGE_DIR/publish"
APP="$STAGE_DIR/Termrig.app"
ICONSET="$STAGE_DIR/Termrig.iconset"

rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR"

echo "Publishing Termrig $VERSION ($RID)..."
dotnet publish "$PROJECT" \
    --configuration Release \
    --runtime "$RID" \
    --self-contained true \
    --output "$PUBLISH_DIR" \
    -p:PackAsTool=false

echo "Generating Termrig.icns..."
mkdir -p "$ICONSET"
for SIZE in 16 32 128 256 512; do
    DOUBLE=$((SIZE * 2))
    sips -z "$SIZE" "$SIZE" "$ICON_SOURCE" --out "$ICONSET/icon_${SIZE}x${SIZE}.png" >/dev/null
    sips -z "$DOUBLE" "$DOUBLE" "$ICON_SOURCE" --out "$ICONSET/icon_${SIZE}x${SIZE}@2x.png" >/dev/null
done

echo "Assembling Termrig.app..."
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBLISH_DIR/." "$APP/Contents/MacOS/"
iconutil --convert icns "$ICONSET" --output "$APP/Contents/Resources/Termrig.icns"
cp "$SCRIPT_DIR/Info.plist" "$APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $VERSION" "$APP/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $VERSION" "$APP/Contents/Info.plist"
chmod +x "$APP/Contents/MacOS/Termrig"
codesign --force --deep --sign - "$APP" >/dev/null 2>&1 || echo "Warning: ad-hoc code signing failed; Gatekeeper may block the app." >&2

echo "Installing to $INSTALL_DIR/Termrig.app..."
mkdir -p "$INSTALL_DIR"
rm -rf "$INSTALL_DIR/Termrig.app"
cp -R "$APP" "$INSTALL_DIR/Termrig.app"
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "$INSTALL_DIR/Termrig.app" >/dev/null 2>&1 || true

echo "Installed $INSTALL_DIR/Termrig.app"
