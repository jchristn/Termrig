#!/usr/bin/env bash
# Rebuild and reinstall Termrig from this checkout: the trig .NET global tool and the
# self-contained desktop app. Succeeds when Termrig is not installed yet. Both are built before
# anything installed is touched, so a failed build leaves the current install in place.
# The app is ~/.local/share/termrig, with a launcher (~/.local/share/applications/
# com.jchristn.Termrig.desktop) and its icon. Opening it on WSL 1 needs an X server and DISPLAY.
# WSL 1 version: installs into this distro only, with the Linux dotnet inside it (never the
# Windows one). Use scripts\windows for the Windows install.
# Refuses while Termrig is running. The tool package is built under artifacts/tools.
# Usage: ./scripts/wsl/reinstall-tool.sh  (-h or --help shows this; no other arguments)
set -euo pipefail

# CDPATH is cleared so an exported CDPATH cannot redirect cd (or make it print the directory).
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

# shellcheck disable=SC2034 # read by _wsl-common.sh
WSL_FLAVOR=wsl
# shellcheck source=_wsl-common.sh
. "$SCRIPT_DIR/_wsl-common.sh"
wsl_require

# Arguments are handled before anything below touches the system, so --help has no side effects.
usage() {
    echo "Usage: ./scripts/wsl/reinstall-tool.sh"
    echo "Rebuilds and reinstalls the trig global tool and the Termrig app. Works when Termrig is not installed."
}
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments)"

# Create ~/.dotnet/tools first so wsl_sanitize_path puts it on PATH for the post-install check.
mkdir -p "$HOME/.dotnet/tools"
wsl_sanitize_path
wsl_check_repo "$REPO_ROOT"

wsl_require_cmd dotnet "Install the .NET 10 SDK inside WSL: https://learn.microsoft.com/dotnet/core/install/linux"
wsl_require_cmd pgrep "Install procps inside WSL (for example: sudo apt install procps)."

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

# The app, its launcher and its icon (the same paths as packaging/linux/build-app.sh).
DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
APP_DIR="$DATA_HOME/termrig"
# shellcheck disable=SC2034 # not every script uses APP_EXE
APP_EXE="$APP_DIR/Termrig"
DESKTOP_FILE="$DATA_HOME/applications/com.jchristn.Termrig.desktop"
ICON_FILE="$DATA_HOME/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg"
app_installed() { [ -e "$APP_DIR" ] || [ -e "$DESKTOP_FILE" ] || [ -e "$ICON_FILE" ]; }

case "$(uname -m)" in
    x86_64|amd64) RID=linux-x64 ;;
    aarch64|arm64) RID=linux-arm64 ;;
    *) die "Unsupported architecture: $(uname -m). Termrig builds for x64 and ARM64." ;;
esac
STAGE_DIR="$REPO_ROOT/artifacts/linux-app/$RID"
PUBLISH_DIR="$STAGE_DIR/publish"

refresh_desktop_caches() {
    if command -v update-desktop-database >/dev/null 2>&1; then
        update-desktop-database "$(dirname "$DESKTOP_FILE")" >/dev/null 2>&1 || true
    fi
    if command -v gtk-update-icon-cache >/dev/null 2>&1; then
        gtk-update-icon-cache -q "$DATA_HOME/icons/hicolor" >/dev/null 2>&1 || true
    fi
}

# Publishes the self-contained app into the staging directory; nothing installed changes.
publish_app() {
    rm -rf "$STAGE_DIR"
    mkdir -p "$STAGE_DIR"
    dotnet publish "$PROJECT" --configuration Release --runtime "$RID" --self-contained true \
        --output "$PUBLISH_DIR" -p:PackAsTool=false
    [ -f "$PUBLISH_DIR/Termrig" ] || die "dotnet publish did not produce $PUBLISH_DIR/Termrig."
}

# Replaces the installed app with the published one and writes the launcher and icon.
install_app() {
    local template="$REPO_ROOT/packaging/linux/com.jchristn.Termrig.desktop"
    local icon="$REPO_ROOT/packaging/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg"
    [ -f "$template" ] || die "Missing $template."
    [ -f "$icon" ] || die "Missing $icon."
    rm -rf "$APP_DIR"
    mkdir -p "$APP_DIR" "$(dirname "$DESKTOP_FILE")" "$(dirname "$ICON_FILE")"
    cp -R "$PUBLISH_DIR/." "$APP_DIR/"
    chmod +x "$APP_EXE"
    cp "$icon" "$ICON_FILE"
    # TERMRIG_FOREGROUND=1 keeps a launcher start as one process, so the dock tracks its window.
    # Desktop Entry spec: inside a quoted argument escape " ` $ \ with a backslash, then double every
    # backslash (string escaping), and write % as %%. awk reads the line from ENVIRON, which (unlike
    # -v) does not interpret backslashes. The template's CRLF line endings, if any, are dropped.
    local quoted="$APP_EXE"
    quoted="${quoted//\\/\\\\}"; quoted="${quoted//\"/\\\"}"; quoted="${quoted//\`/\\\`}"; quoted="${quoted//\$/\\\$}"
    quoted="${quoted//\\/\\\\}"; quoted="${quoted//%/%%}"
    TERMRIG_EXEC_LINE="Exec=env TERMRIG_FOREGROUND=1 \"$quoted\"" \
        awk '{ sub(/\r$/, "") } /^Exec=/ { print ENVIRON["TERMRIG_EXEC_LINE"]; next } { print }' "$template" > "$DESKTOP_FILE"
    chmod 644 "$DESKTOP_FILE"
    refresh_desktop_caches
    echo "Installed $APP_DIR, $DESKTOP_FILE and $ICON_FILE"
}

echo "Termrig reinstall (WSL 1, $VERSION, $RID)"
echo "[1/7] Checking for running Termrig processes..."
if termrig_running; then
    # kill-termrig would close this very tab, so point at another terminal instead.
    if [ "${TERM_PROGRAM:-}" = Termrig ]; then
        die "Termrig is running, and this terminal is a Termrig tab. Quit Termrig and run this from another terminal."
    fi
    die "Termrig is running. Quit every Termrig window and trig session (or run scripts/wsl/kill-termrig.sh) and try again."
fi
wsl_ensure_gui_libs warning
echo "[2/7] Building the Termrig tool package..."
rm -rf "$PACKAGE_SOURCE" "$NUGET_CACHE"
mkdir -p "$PACKAGE_SOURCE"
dotnet pack "$PROJECT" --configuration Release --output "$PACKAGE_SOURCE"
echo "[3/7] Publishing the Termrig app ($RID)..."
publish_app
echo "[4/7] Removing the existing install..."
if tool_installed; then
    dotnet tool uninstall -g Termrig
else
    echo "The Termrig global tool is not installed; continuing."
fi
echo "[5/7] Installing the Termrig global tool..."
dotnet tool install -g Termrig --version "$VERSION" --source "$PACKAGE_SOURCE" --no-http-cache
echo "[6/7] Installing the Termrig app..."
install_app
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
[ -x "$APP_EXE" ] || die "The Termrig app was not installed at $APP_EXE."
# 'dotnet tool install -g' puts trig in ~/.dotnet/tools, and a user-local .NET in ~/.dotnet also
# needs DOTNET_ROOT, or new shells cannot run trig.
case ":$WSL_ORIGINAL_PATH:" in
    *":$HOME/.dotnet/tools:"*) ;;
    *)
        echo "Note: $HOME/.dotnet/tools is not on your PATH, so new shells will not find 'trig'." >&2
        echo "Add this line to ~/.bashrc (or ~/.profile):" >&2
        echo '  export PATH="$PATH:$HOME/.dotnet/tools"' >&2 ;;
esac
if [ -z "$WSL_ORIGINAL_DOTNET_ROOT" ] && [ "$(command -v dotnet)" = "$HOME/.dotnet/dotnet" ]; then
    echo "Note: .NET is installed in ~/.dotnet, so 'trig' also needs DOTNET_ROOT. Add this line to ~/.bashrc (or ~/.profile):" >&2
    echo '  export DOTNET_ROOT="$HOME/.dotnet"' >&2
fi
if [ -z "${DISPLAY:-}" ]; then
    echo "Note: DISPLAY is not set. WSL 1 has no WSLg, so opening Termrig needs an X server on Windows and DISPLAY set (for example: export DISPLAY=:0)." >&2
fi
echo "Done. Start Termrig with 'trig' or with scripts/wsl/run.sh."
