#!/usr/bin/env sh
# Refresh the Termrig app inside this distro, then launch it. WSL 1 counterpart of scripts/linux/run.sh.
# Termrig is a desktop app and WSL 1 has no GUI support, so this fails on WSL 1; run the scripts/windows version from Windows, or convert the distro with 'wsl --set-version <distro> 2' and use scripts/wsl2.
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
if [ "$(wsl_version)" = 1 ]; then
    wsl_die "Termrig is a desktop app and WSL 1 has no GUI support. Run the scripts\\windows version from Windows, or convert this distro with 'wsl --set-version ${WSL_DISTRO_NAME:-<distro>} 2' (from Windows) and use scripts/wsl2."
fi
wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"
if [ -z "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]; then
    wsl_die "No display found. Termrig is a desktop app and needs WSLg here. Run 'wsl --update' and then 'wsl --shutdown' from Windows, reopen this distro, or use scripts\\windows\\run.bat from Windows."
fi
wsl_run "$SCRIPT_DIR/../linux/run.sh" "$@"
