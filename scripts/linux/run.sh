#!/usr/bin/env bash
# Rebuild the self-contained Termrig app from this checkout, install it over the current copy,
# and open the installed copy. Does not touch the trig global tool. Refuses while Termrig is
# running, because the installed app is replaced.
# On Linux the app is ~/.local/share/termrig, with an application-menu launcher
# (~/.local/share/applications/com.jchristn.Termrig.desktop) and its icon.
# Usage: ./scripts/linux/run.sh  (-h or --help shows this; no other arguments)
set -euo pipefail

# CDPATH is cleared so an exported CDPATH cannot redirect cd (or make it print the directory).
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

if [ "$(uname -s)" != Linux ]; then
    die "This script is for Linux. Use scripts/macos on macOS or scripts\\windows on Windows."
fi
# A Microsoft kernel alone is not WSL: Docker Desktop containers have one too, but no WSL interop.
if grep -qi microsoft /proc/version 2>/dev/null \
    && { [ -n "${WSL_DISTRO_NAME:-}" ] || [ -d /run/WSL ] || ls /proc/sys/fs/binfmt_misc/WSLInterop* >/dev/null 2>&1; }; then
    die "This is WSL. Use the same script in scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1); they handle the Windows PATH, line endings and the WSL display."
fi

usage() {
    echo "Usage: ./scripts/linux/run.sh"
    echo "Rebuilds and reinstalls the Termrig app, then opens it."
}
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments)"

command -v dotnet >/dev/null 2>&1 || die "dotnet was not found on PATH. Install the .NET 10 SDK: https://learn.microsoft.com/dotnet/core/install/linux"
command -v pgrep >/dev/null 2>&1 || die "pgrep was not found on PATH. Install procps (for example: sudo apt install procps)."

if [ -z "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]; then
    die "Termrig is a desktop app and needs a graphical session (neither DISPLAY nor WAYLAND_DISPLAY is set)."
fi

PROJECT="$REPO_ROOT/src/Termrig.App/Termrig.App.csproj"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -n 1)"
[ -n "$VERSION" ] || die "Cannot read <Version> from $PROJECT."

# Process names: trig (the global tool) and Termrig (the installed app, or a dotnet run build);
# Termrig.dll matches 'dotnet Termrig.dll'. Only this user's processes count.
CURRENT_UID="$(id -u)"
termrig_running() {
    pgrep -u "$CURRENT_UID" -x trig >/dev/null 2>&1 && return 0
    pgrep -u "$CURRENT_UID" -x Termrig >/dev/null 2>&1 && return 0
    pgrep -u "$CURRENT_UID" -f 'Termrig\.dll' >/dev/null 2>&1
}

# The app, its launcher and its icon (the same paths as packaging/linux/build-app.sh).
DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
APP_DIR="$DATA_HOME/termrig"
# shellcheck disable=SC2034 # not every script uses APP_EXE
APP_EXE="$APP_DIR/Termrig"
DESKTOP_FILE="$DATA_HOME/applications/com.jchristn.Termrig.desktop"
ICON_FILE="$DATA_HOME/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg"
app_installed() { [ -e "$APP_DIR" ] || [ -e "$DESKTOP_FILE" ] || [ -e "$ICON_FILE" ]; }

case "$(uname -m)" in
    x86_64|amd64) RID=linux-x64 ;;
    aarch64|arm64) RID=linux-arm64 ;;
    *) die "Unsupported architecture: $(uname -m). Termrig builds for x64 and ARM64." ;;
esac
STAGE_DIR="$REPO_ROOT/artifacts/linux-app/$RID"
PUBLISH_DIR="$STAGE_DIR/publish"

refresh_desktop_caches() {
    if command -v update-desktop-database >/dev/null 2>&1; then
        update-desktop-database "$(dirname "$DESKTOP_FILE")" >/dev/null 2>&1 || true
    fi
    if command -v gtk-update-icon-cache >/dev/null 2>&1; then
        gtk-update-icon-cache -q "$DATA_HOME/icons/hicolor" >/dev/null 2>&1 || true
    fi
}

# Publishes the self-contained app into the staging directory; nothing installed changes.
publish_app() {
    rm -rf "$STAGE_DIR"
    mkdir -p "$STAGE_DIR"
    dotnet publish "$PROJECT" --configuration Release --runtime "$RID" --self-contained true \
        --output "$PUBLISH_DIR" -p:PackAsTool=false
    [ -f "$PUBLISH_DIR/Termrig" ] || die "dotnet publish did not produce $PUBLISH_DIR/Termrig."
}

# Replaces the installed app with the published one and writes the launcher and icon.
install_app() {
    local template="$REPO_ROOT/packaging/linux/com.jchristn.Termrig.desktop"
    local icon="$REPO_ROOT/packaging/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg"
    [ -f "$template" ] || die "Missing $template."
    [ -f "$icon" ] || die "Missing $icon."
    rm -rf "$APP_DIR"
    mkdir -p "$APP_DIR" "$(dirname "$DESKTOP_FILE")" "$(dirname "$ICON_FILE")"
    cp -R "$PUBLISH_DIR/." "$APP_DIR/"
    chmod +x "$APP_EXE"
    cp "$icon" "$ICON_FILE"
    # TERMRIG_FOREGROUND=1 keeps a launcher start as one process, so the dock tracks its window.
    # Desktop Entry spec: inside a quoted argument escape " ` $ \ with a backslash, then double every
    # backslash (string escaping), and write % as %%. awk reads the line from ENVIRON, which (unlike
    # -v) does not interpret backslashes. The template's CRLF line endings, if any, are dropped.
    local quoted="$APP_EXE"
    quoted="${quoted//\\/\\\\}"; quoted="${quoted//\"/\\\"}"; quoted="${quoted//\`/\\\`}"; quoted="${quoted//\$/\\\$}"
    quoted="${quoted//\\/\\\\}"; quoted="${quoted//%/%%}"
    TERMRIG_EXEC_LINE="Exec=env TERMRIG_FOREGROUND=1 \"$quoted\"" \
        awk '{ sub(/\r$/, "") } /^Exec=/ { print ENVIRON["TERMRIG_EXEC_LINE"]; next } { print }' "$template" > "$DESKTOP_FILE"
    chmod 644 "$DESKTOP_FILE"
    refresh_desktop_caches
    echo "Installed $APP_DIR, $DESKTOP_FILE and $ICON_FILE"
}

echo "Termrig run (Linux, $VERSION, $RID)"
echo "[1/4] Checking for running Termrig processes..."
if termrig_running; then
    # kill-termrig would close this very tab, so point at another terminal instead.
    if [ "${TERM_PROGRAM:-}" = Termrig ]; then
        die "Termrig is running, and this terminal is a Termrig tab. Quit Termrig and run this from another terminal."
    fi
    die "Termrig is running. Quit every Termrig window and trig session (or run scripts/linux/kill-termrig.sh) and try again."
fi
echo "[2/4] Publishing the Termrig app ($RID)..."
publish_app
echo "[3/4] Installing the Termrig app..."
install_app
echo "[4/4] Opening Termrig..."
[ -x "$APP_EXE" ] || die "The Termrig app was not installed at $APP_EXE."
# Termrig detaches from the terminal on its own.
exec "$APP_EXE"
