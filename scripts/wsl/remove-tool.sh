#!/usr/bin/env sh
# Remove the trig command and the Termrig app from this distro. WSL 1 counterpart of scripts/linux/remove-tool.sh.
# Works on WSL 1 too (it only deletes files).
set -eu
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
WSL_FLAVOR=wsl
# shellcheck source-path=SCRIPTDIR source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"
# The root scripts and packaging/linux/build-app.sh live outside scripts/, so check them for CRLF too.
if grep -l "$(printf '\r')" "$REPO_ROOT"/*.sh "$REPO_ROOT/packaging/linux/build-app.sh" >/dev/null 2>&1; then
    wsl_die "The root .sh scripts have Windows (CRLF) line endings. Re-checkout with LF endings: git config core.autocrlf input && git rm -r --cached -q . && git reset --hard"
fi
wsl_run "$SCRIPT_DIR/../linux/remove-tool.sh" "$@"
