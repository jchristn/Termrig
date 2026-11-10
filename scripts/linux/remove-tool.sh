#!/usr/bin/env bash
# Remove everything install-tool.sh installs: the trig .NET global tool and the desktop app.
# Succeeds with a message when nothing is installed. Refuses while Termrig is running.
# On Linux it deletes ~/.local/share/termrig, the launcher and the icon.
# Your profiles and settings in ~/.termrig are kept.
# Usage: ./scripts/linux/remove-tool.sh  (-h or --help shows this; no other arguments)
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
    echo "Usage: ./scripts/linux/remove-tool.sh"
    echo "Removes the trig global tool and the Termrig app. Succeeds when nothing is installed."
}
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments)"

command -v dotnet >/dev/null 2>&1 || die "dotnet was not found on PATH. Install the .NET 10 SDK: https://learn.microsoft.com/dotnet/core/install/linux"
command -v pgrep >/dev/null 2>&1 || die "pgrep was not found on PATH. Install procps (for example: sudo apt install procps)."

# Process names: trig (the global tool) and Termrig (the installed app, or a dotnet run build);
# Termrig.dll matches 'dotnet Termrig.dll'. Only this user's processes count.
CURRENT_UID="$(id -u)"
termrig_running() {
    pgrep -u "$CURRENT_UID" -x trig >/dev/null 2>&1 && return 0
    pgrep -u "$CURRENT_UID" -x Termrig >/dev/null 2>&1 && return 0
    pgrep -u "$CURRENT_UID" -f 'Termrig\.dll' >/dev/null 2>&1
}

# True when the Termrig global tool (the trig command) is installed.
tool_installed() {
    dotnet tool list -g 2>/dev/null | awk 'tolower($1) == "termrig" { found = 1 } END { exit !found }'
}

# The app, its launcher and its icon (the same paths as packaging/linux/build-app.sh).
DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
APP_DIR="$DATA_HOME/termrig"
# shellcheck disable=SC2034 # not every script uses APP_EXE
APP_EXE="$APP_DIR/Termrig"
DESKTOP_FILE="$DATA_HOME/applications/com.jchristn.Termrig.desktop"
ICON_FILE="$DATA_HOME/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg"
app_installed() { [ -e "$APP_DIR" ] || [ -e "$DESKTOP_FILE" ] || [ -e "$ICON_FILE" ]; }

refresh_desktop_caches() {
    if command -v update-desktop-database >/dev/null 2>&1; then
        update-desktop-database "$(dirname "$DESKTOP_FILE")" >/dev/null 2>&1 || true
    fi
    if command -v gtk-update-icon-cache >/dev/null 2>&1; then
        gtk-update-icon-cache -q "$DATA_HOME/icons/hicolor" >/dev/null 2>&1 || true
    fi
}

# Deletes the app, its launcher and its icon. Prints what it removed.
remove_app() {
    local item
    for item in "$APP_DIR" "$DESKTOP_FILE" "$ICON_FILE"; do
        if [ -e "$item" ]; then
            rm -rf "$item"
            echo "Removed $item"
        fi
    done
    refresh_desktop_caches
}

echo "Termrig removal (Linux)"
echo "[1/3] Checking for running Termrig processes..."
if termrig_running; then
    # kill-termrig would close this very tab, so point at another terminal instead.
    if [ "${TERM_PROGRAM:-}" = Termrig ]; then
        die "Termrig is running, and this terminal is a Termrig tab. Quit Termrig and run this from another terminal."
    fi
    die "Termrig is running. Quit every Termrig window and trig session (or run scripts/linux/kill-termrig.sh) and try again."
fi
REMOVED=0
echo "[2/3] Removing the Termrig global tool..."
if tool_installed; then
    dotnet tool uninstall -g Termrig
    REMOVED=1
else
    echo "The Termrig global tool is not installed."
fi
echo "[3/3] Removing the Termrig app..."
if app_installed; then
    remove_app
    REMOVED=1
else
    echo "The Termrig app is not installed."
fi
if [ "$REMOVED" = 0 ]; then
    echo "Termrig is not installed; nothing to remove."
fi
echo "Done. Your profiles and settings in ~/.termrig were kept."
