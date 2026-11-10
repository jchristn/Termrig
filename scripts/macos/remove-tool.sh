#!/usr/bin/env bash
# Remove everything install-tool.sh installs: the trig .NET global tool and the desktop app.
# Succeeds with a message when nothing is installed. Refuses while Termrig is running.
# On macOS it deletes Termrig.app from /Applications and ~/Applications and unregisters it.
# Your profiles and settings in ~/.termrig are kept.
# Usage: ./scripts/macos/remove-tool.sh  (-h or --help shows this; no other arguments)
set -euo pipefail

# CDPATH is cleared so an exported CDPATH cannot redirect cd (or make it print the directory).
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

if [ "$(uname -s)" != Darwin ]; then
    die "This script is for macOS. Use scripts/linux on Linux, scripts/wsl2 (or scripts/wsl) on WSL, or scripts\\windows on Windows."
fi

usage() {
    echo "Usage: ./scripts/macos/remove-tool.sh"
    echo "Removes the trig global tool and the Termrig app. Succeeds when nothing is installed."
}
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments)"

# shellcheck source=app-bundle.sh
. "$SCRIPT_DIR/app-bundle.sh"
command -v dotnet >/dev/null 2>&1 || die "dotnet was not found on PATH. Install the .NET 10 SDK: https://dotnet.microsoft.com/download"
command -v pgrep >/dev/null 2>&1 || die "pgrep was not found on PATH. pgrep ships with macOS in /usr/bin; check your PATH."

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

macos_app_paths

echo "Termrig removal (macOS)"
echo "[1/3] Checking for running Termrig processes..."
if termrig_running; then
    # kill-termrig would close this very tab, so point at another terminal instead.
    if [ "${TERM_PROGRAM:-}" = Termrig ]; then
        die "Termrig is running, and this terminal is a Termrig tab. Quit Termrig and run this from another terminal."
    fi
    die "Termrig is running. Quit every Termrig window and trig session (or run scripts/macos/kill-termrig.sh) and try again."
fi
REMOVED=0
echo "[2/3] Removing the Termrig global tool..."
if tool_installed; then
    dotnet tool uninstall -g Termrig
    REMOVED=1
else
    echo "The Termrig global tool is not installed."
fi
echo "[3/3] Removing Termrig.app..."
if macos_app_installed; then
    remove_macos_app
    REMOVED=1
else
    echo "The Termrig app is not installed."
fi
if [ "$REMOVED" = 0 ]; then
    echo "Termrig is not installed; nothing to remove."
fi
echo "Done. Your profiles and settings in ~/.termrig were kept."
