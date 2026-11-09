#!/usr/bin/env sh
# Linux entry point; the root script installs the trig command and the application-menu app.
# Run through sh so a checkout without executable bits still works.
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
[ "$(uname -s)" = Linux ] || { echo "This script requires Linux." >&2; exit 1; }
exec sh "$SCRIPT_DIR/../../install-tool.sh" "$@"
