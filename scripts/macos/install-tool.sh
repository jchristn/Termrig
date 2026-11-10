#!/usr/bin/env bash
# Build and install Termrig from this checkout: the trig .NET global tool and the
# self-contained desktop app. Fails if either is already installed; use reinstall-tool.sh
# to replace an existing install.
# On macOS the app is Termrig.app in /Applications (or ~/Applications when /Applications is
# not writable); a copy left in the other location is removed so Spotlight opens this build.
# Refuses while Termrig is running. The tool package is built under artifacts/tools.
# Usage: ./scripts/macos/install-tool.sh  (-h or --help shows this; no other arguments)
set -euo pipefail

# CDPATH is cleared so an exported CDPATH cannot redirect cd (or make it print the directory).
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

# Captured before anything below changes them, for the PATH and DOTNET_ROOT notes at the end.
ORIGINAL_DOTNET_ROOT="${DOTNET_ROOT:-}"
ORIGINAL_PATH="$PATH"

if [ "$(uname -s)" != Darwin ]; then
    die "This script is for macOS. Use scripts/linux on Linux, scripts/wsl2 (or scripts/wsl) on WSL, or scripts\\windows on Windows."
fi

usage() {
    echo "Usage: ./scripts/macos/install-tool.sh"
    echo "Builds and installs the trig global tool and the Termrig app. Fails if Termrig is already installed; use reinstall-tool.sh."
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

PROJECT="$REPO_ROOT/src/Termrig.App/Termrig.App.csproj"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -n 1)"
[ -n "$VERSION" ] || die "Cannot read <Version> from $PROJECT."
PACKAGE_SOURCE="$REPO_ROOT/artifacts/tools"
# dotnet tool install reuses a cached package with the same version, so the cached copy is dropped.
NUGET_CACHE="${NUGET_PACKAGES:-$HOME/.nuget/packages}/termrig/$VERSION"

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
macos_rid

echo "Termrig install (macOS, $VERSION, $RID)"
echo "[1/7] Checking for running Termrig processes..."
if termrig_running; then
    # kill-termrig would close this very tab, so point at another terminal instead.
    if [ "${TERM_PROGRAM:-}" = Termrig ]; then
        die "Termrig is running, and this terminal is a Termrig tab. Quit Termrig and run this from another terminal."
    fi
    die "Termrig is running. Quit every Termrig window and trig session (or run scripts/macos/kill-termrig.sh) and try again."
fi
echo "[2/7] Checking for an existing install..."
if tool_installed; then
    die "The Termrig global tool (trig) is already installed. Run scripts/macos/reinstall-tool.sh to replace it."
fi
if macos_app_installed; then
    die "The Termrig app is already installed. Run scripts/macos/reinstall-tool.sh to replace it."
fi
echo "[3/7] Building the Termrig tool package..."
rm -rf "$PACKAGE_SOURCE" "$NUGET_CACHE"
mkdir -p "$PACKAGE_SOURCE"
dotnet pack "$PROJECT" --configuration Release --output "$PACKAGE_SOURCE"
echo "[4/7] Publishing the Termrig app ($RID)..."
build_macos_app
echo "[5/7] Installing the Termrig global tool..."
dotnet tool install -g Termrig --version "$VERSION" --source "$PACKAGE_SOURCE" --no-http-cache
echo "[6/7] Installing Termrig.app..."
install_macos_app
echo "[7/7] Verifying the install..."
tool_installed || die "Termrig is not listed by 'dotnet tool list -g' after the install."
# trig opens the app, so it is located rather than run.
if command -v trig >/dev/null 2>&1; then
    echo "trig: $(command -v trig)"
elif [ -x "$HOME/.dotnet/tools/trig" ]; then
    echo "trig: $HOME/.dotnet/tools/trig"
else
    die "Termrig was installed, but trig is not on PATH or in $HOME/.dotnet/tools."
fi
macos_app_installed || die "Termrig.app was not installed in $INSTALL_DIR."
# 'dotnet tool install -g' puts trig in ~/.dotnet/tools, and a user-local .NET in ~/.dotnet also
# needs DOTNET_ROOT, or new shells cannot run trig.
case ":$ORIGINAL_PATH:" in
    *":$HOME/.dotnet/tools:"*) ;;
    *)
        echo "Note: $HOME/.dotnet/tools is not on your PATH, so new shells will not find 'trig'." >&2
        echo "Add this line to ~/.zprofile:" >&2
        echo '  export PATH="$PATH:$HOME/.dotnet/tools"' >&2 ;;
esac
if [ -z "$ORIGINAL_DOTNET_ROOT" ] && [ "$(command -v dotnet)" = "$HOME/.dotnet/dotnet" ]; then
    echo "Note: .NET is installed in ~/.dotnet, so 'trig' also needs DOTNET_ROOT. Add this line to ~/.zprofile:" >&2
    echo '  export DOTNET_ROOT="$HOME/.dotnet"' >&2
fi
echo "Done. Start Termrig with 'trig' or from Spotlight or the Applications folder."
