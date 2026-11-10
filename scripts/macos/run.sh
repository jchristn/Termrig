#!/usr/bin/env bash
# Rebuild the self-contained Termrig app from this checkout, install it over the current copy,
# and open the installed copy. Does not touch the trig global tool. Refuses while Termrig is
# running, because the installed app is replaced.
# On macOS the app is Termrig.app in /Applications (or ~/Applications when /Applications is
# not writable); a copy left in the other location is removed so Spotlight opens this build.
# Usage: ./scripts/macos/run.sh  (-h or --help shows this; no other arguments)
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
    echo "Usage: ./scripts/macos/run.sh"
    echo "Rebuilds and reinstalls the Termrig app, then opens it."
}
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments)"

# shellcheck source=app-bundle.sh
. "$SCRIPT_DIR/app-bundle.sh"
command -v dotnet >/dev/null 2>&1 || die "dotnet was not found on PATH. Install the .NET 10 SDK: https://dotnet.microsoft.com/download"
command -v pgrep >/dev/null 2>&1 || die "pgrep was not found on PATH. pgrep ships with macOS in /usr/bin; check your PATH."
macos_require_app_tools
command -v open >/dev/null 2>&1 || die "open was not found on PATH; it ships with macOS in /usr/bin."

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

macos_app_paths
macos_rid

echo "Termrig run (macOS, $VERSION, $RID)"
echo "[1/4] Checking for running Termrig processes..."
if termrig_running; then
    # kill-termrig would close this very tab, so point at another terminal instead.
    if [ "${TERM_PROGRAM:-}" = Termrig ]; then
        die "Termrig is running, and this terminal is a Termrig tab. Quit Termrig and run this from another terminal."
    fi
    die "Termrig is running. Quit every Termrig window and trig session (or run scripts/macos/kill-termrig.sh) and try again."
fi
echo "[2/4] Publishing the Termrig app ($RID)..."
build_macos_app
echo "[3/4] Installing Termrig.app..."
install_macos_app
echo "[4/4] Opening Termrig..."
open -a "$INSTALL_DIR/Termrig.app"
