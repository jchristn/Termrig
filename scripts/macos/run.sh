#!/usr/bin/env sh
# Refresh the self-contained Finder app, then launch that installed copy.
set -eu
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
[ "$(uname -s)" = Darwin ] || { echo "This script requires macOS." >&2; exit 1; }
[ "$#" -eq 0 ] || { echo "Usage: $0" >&2; exit 1; }
"$SCRIPT_DIR/../../packaging/macos/build-app.sh"
open -a "$HOME/Applications/Termrig.app"
