#!/usr/bin/env sh
# Refresh the self-contained app in ~/.local/share/termrig, then launch that installed copy.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
[ "$(uname -s)" = Linux ] || { echo "This script requires Linux." >&2; exit 1; }
[ "$#" -eq 0 ] || { echo "Usage: $0" >&2; exit 1; }
if [ -z "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]; then
    echo "Termrig is a desktop app and needs a graphical session (neither DISPLAY nor WAYLAND_DISPLAY is set)." >&2
    exit 1
fi
sh "$SCRIPT_DIR/../../packaging/linux/build-app.sh"
APP="${XDG_DATA_HOME:-$HOME/.local/share}/termrig/Termrig"
[ -x "$APP" ] || { echo "Termrig was not installed at $APP." >&2; exit 1; }
# Termrig detaches from the terminal on its own.
exec "$APP"
