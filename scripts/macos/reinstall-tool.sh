#!/usr/bin/env sh
# macOS entry point; the root script installs the CLI and Finder application.
set -eu
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
[ "$(uname -s)" = Darwin ] || { echo "This script requires macOS." >&2; exit 1; }
exec "$SCRIPT_DIR/../../reinstall-tool.sh" "$@"
