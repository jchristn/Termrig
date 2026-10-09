#!/usr/bin/env sh
# Build Termrig for this machine and install it for the current user, with an
# application-menu launcher so it can be pinned to the dock.
#
# Usage: packaging/linux/build-app.sh
#
# Installs:
#   ~/.local/share/termrig/                                      the app (self-contained)
#   ~/.local/share/applications/com.jchristn.Termrig.desktop     the launcher
#   ~/.local/share/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg
#
# No sudo needed. Uninstall with remove-tool.sh, or delete those three paths.
set -eu

if [ "$(uname -s)" != "Linux" ]; then
    echo "build-app.sh: this script installs Termrig on Linux; skipping." >&2
    exit 0
fi

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/src/Termrig.App/Termrig.App.csproj"
DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
APP_DIR="$DATA_HOME/termrig"
DESKTOP_DIR="$DATA_HOME/applications"
DESKTOP_FILE="$DESKTOP_DIR/com.jchristn.Termrig.desktop"
ICON_DIR="$DATA_HOME/icons/hicolor/scalable/apps"

case "$(uname -m)" in
    aarch64|arm64) RID="linux-arm64" ;;
    *) RID="linux-x64" ;;
esac

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -n 1)"
STAGE_DIR="$REPO_ROOT/artifacts/linux-app/$RID"
PUBLISH_DIR="$STAGE_DIR/publish"

if pgrep -f "$APP_DIR/Termrig" >/dev/null 2>&1; then
    echo "Termrig is running from $APP_DIR. Quit it and run this script again." >&2
    exit 1
fi

rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR"

echo "Publishing Termrig $VERSION ($RID)..."
dotnet publish "$PROJECT" \
    --configuration Release \
    --runtime "$RID" \
    --self-contained true \
    --output "$PUBLISH_DIR" \
    -p:PackAsTool=false

echo "Installing to $APP_DIR..."
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR"
cp -R "$PUBLISH_DIR/." "$APP_DIR/"
chmod +x "$APP_DIR/Termrig"

echo "Installing the launcher and icon..."
mkdir -p "$DESKTOP_DIR" "$ICON_DIR"
cp "$REPO_ROOT/packaging/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg" "$ICON_DIR/com.jchristn.Termrig.svg"
# TERMRIG_FOREGROUND=1 keeps a launcher start as a single process (no detach), so the
# dock tracks the window it started.
sed -e "s|^Exec=.*|Exec=env TERMRIG_FOREGROUND=1 \"$APP_DIR/Termrig\"|" \
    "$REPO_ROOT/packaging/linux/com.jchristn.Termrig.desktop" > "$DESKTOP_FILE"
chmod 644 "$DESKTOP_FILE"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q "$DATA_HOME/icons/hicolor" >/dev/null 2>&1 || true
fi

echo "Installed Termrig $VERSION. Open it from your application menu (search for \"Termrig\")."
