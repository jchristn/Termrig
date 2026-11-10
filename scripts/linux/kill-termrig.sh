#!/usr/bin/env bash
# Stop every running Termrig process: the installed app, the trig global tool and copies started
# with dotnet run or dotnet Termrig.dll. Asks them to exit, waits up to 5 seconds, then
# force-kills what is left. Succeeds when nothing is running. Every terminal tab inside Termrig
# closes with it, including this one if you run it from a Termrig tab.
# Usage: ./scripts/linux/kill-termrig.sh  (-h or --help shows this; no other arguments)
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
    echo "Usage: ./scripts/linux/kill-termrig.sh"
    echo "Stops every running Termrig process. Succeeds when nothing is running."
}
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments)"

command -v pgrep >/dev/null 2>&1 || die "pgrep was not found on PATH. Install procps (for example: sudo apt install procps)."
command -v pkill >/dev/null 2>&1 || die "pkill was not found on PATH. Install procps (for example: sudo apt install procps)."

# Process names: trig (the global tool) and Termrig (the installed app, or a dotnet run build);
# Termrig.dll matches 'dotnet Termrig.dll'. Only this user's processes count.
CURRENT_UID="$(id -u)"
termrig_running() {
    pgrep -u "$CURRENT_UID" -x trig >/dev/null 2>&1 && return 0
    pgrep -u "$CURRENT_UID" -x Termrig >/dev/null 2>&1 && return 0
    pgrep -u "$CURRENT_UID" -f 'Termrig\.dll' >/dev/null 2>&1
}

signal_all() {
    pkill "$1" -u "$CURRENT_UID" -x trig >/dev/null 2>&1 || true
    pkill "$1" -u "$CURRENT_UID" -x Termrig >/dev/null 2>&1 || true
    pkill "$1" -u "$CURRENT_UID" -f 'Termrig\.dll' >/dev/null 2>&1 || true
}

echo "Stopping Termrig processes..."
if ! termrig_running; then
    echo "No Termrig processes are running."
    exit 0
fi
signal_all -TERM
for _ in 1 2 3 4 5; do
    termrig_running || break
    sleep 1
done
if termrig_running; then
    echo "Some Termrig processes did not exit; force-killing them."
    signal_all -KILL
    sleep 1
fi
termrig_running && die "Some Termrig processes could not be stopped. Check: pgrep -u $CURRENT_UID -fl Termrig"
echo "Done."
