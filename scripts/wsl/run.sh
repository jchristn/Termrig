#!/usr/bin/env bash
# Rebuild the self-contained Termrig app from this checkout, install it over the current copy,
# and open the installed copy. Does not touch the trig global tool. Refuses while Termrig is
# running, because the installed app is replaced.
# The app is ~/.local/share/termrig, with a launcher (~/.local/share/applications/
# com.jchristn.Termrig.desktop) and its icon. Opening it on WSL 1 needs an X server and DISPLAY.
# WSL 1 version: needs an X server on Windows with DISPLAY set; WSL 1 has no WSLg.
# Usage: ./scripts/wsl/run.sh  (-h or --help shows this; no other arguments)
set -euo pipefail

# CDPATH is cleared so an exported CDPATH cannot redirect cd (or make it print the directory).
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require

# Arguments are handled before anything below touches the system, so --help has no side effects.
usage() {
    echo "Usage: ./scripts/wsl/run.sh"
    echo "Rebuilds and reinstalls the Termrig app, then opens it."
}
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments)"

wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"
wsl_require_cmd pgrep "Install procps inside WSL (for example: sudo apt install procps)."

if [ -z "${DISPLAY:-}" ]; then
    die "DISPLAY is not set. WSL 1 has no WSLg, so Termrig needs an X server running on Windows (for example VcXsrv) and DISPLAY set (for example: export DISPLAY=:0). Or run scripts\\windows\\run.bat from Windows, or move this distro to WSL 2 (wsl --set-version ${WSL_DISTRO_NAME:-<distro>} 2) and use scripts/wsl2."
fi
wsl_check_gui_libs error

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

echo "Termrig run (WSL 1, $VERSION, $RID)"
echo "[1/4] Checking for running Termrig processes..."
if termrig_running; then
    # kill-termrig would close this very tab, so point at another terminal instead.
    if [ "${TERM_PROGRAM:-}" = Termrig ]; then
        die "Termrig is running, and this terminal is a Termrig tab. Quit Termrig and run this from another terminal."
    fi
    die "Termrig is running. Quit every Termrig window and trig session (or run scripts/wsl/kill-termrig.sh) and try again."
fi
echo "[2/4] Publishing the Termrig app ($RID)..."
publish_app
echo "[3/4] Installing the Termrig app..."
install_app
echo "[4/4] Opening Termrig..."
[ -x "$APP_EXE" ] || die "The Termrig app was not installed at $APP_EXE."
# Termrig detaches from the terminal on its own.
# Termrig's tabs inherit its environment, so it gets the PATH and DOTNET_ROOT from before
# wsl_sanitize_path: tabs keep the Windows PATH (code, explorer.exe, clip.exe, wsl.exe).
if [ -n "$WSL_ORIGINAL_DOTNET_ROOT" ]; then
    exec env PATH="$WSL_ORIGINAL_PATH" DOTNET_ROOT="$WSL_ORIGINAL_DOTNET_ROOT" "$APP_EXE"
fi
exec env -u DOTNET_ROOT PATH="$WSL_ORIGINAL_PATH" "$APP_EXE"
