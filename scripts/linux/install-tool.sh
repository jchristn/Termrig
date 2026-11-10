#!/usr/bin/env bash
# Build and install Termrig from this checkout: the trig .NET global tool and the
# self-contained desktop app. Fails if either is already installed; use reinstall-tool.sh
# to replace an existing install.
# On Linux the app is ~/.local/share/termrig, with an application-menu launcher
# (~/.local/share/applications/com.jchristn.Termrig.desktop) and its icon.
# Refuses while Termrig is running. The tool package is built under artifacts/tools.
# Usage: ./scripts/linux/install-tool.sh  (-h or --help shows this; no other arguments)
set -euo pipefail

# CDPATH is cleared so an exported CDPATH cannot redirect cd (or make it print the directory).
SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck disable=SC2034 # not every script needs REPO_ROOT
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
die() { echo "Error: $*" >&2; exit 1; }

# Captured before anything below changes them, for the PATH and DOTNET_ROOT notes at the end.
ORIGINAL_DOTNET_ROOT="${DOTNET_ROOT:-}"
ORIGINAL_PATH="$PATH"

if [ "$(uname -s)" != Linux ]; then
    die "This script is for Linux. Use scripts/macos on macOS or scripts\\windows on Windows."
fi
# A Microsoft kernel alone is not WSL: Docker Desktop containers have one too, but no WSL interop.
if grep -qi microsoft /proc/version 2>/dev/null \
    && { [ -n "${WSL_DISTRO_NAME:-}" ] || [ -d /run/WSL ] || ls /proc/sys/fs/binfmt_misc/WSLInterop* >/dev/null 2>&1; }; then
    die "This is WSL. Use the same script in scripts/wsl2 (WSL 2) or scripts/wsl (WSL 1); they handle the Windows PATH, line endings and the WSL display."
fi

usage() {
    echo "Usage: ./scripts/linux/install-tool.sh"
    echo "Builds and installs the trig global tool and the Termrig app. Fails if Termrig is already installed; use reinstall-tool.sh."
}
case "${1:-}" in
    -h|--help) usage; exit 0 ;;
esac
[ "$#" -eq 0 ] || die "Unexpected argument: $1 (this script takes no arguments)"

command -v dotnet >/dev/null 2>&1 || die "dotnet was not found on PATH. Install the .NET 10 SDK: https://learn.microsoft.com/dotnet/core/install/linux"
command -v pgrep >/dev/null 2>&1 || die "pgrep was not found on PATH. Install procps (for example: sudo apt install procps)."

# Shared libraries Termrig loads when it opens its window: Avalonia's X11 backend (libX11, libICE,
# libSM, libXext, libXrandr, libXi, libXcursor, libXfixes) and SkiaSharp's fontconfig. Building does
# not need them, but without them Termrig stops at startup with "Unable to load shared library
# 'libICE.so.6'" (or another name below). Desktop installs have them; WSL distros and minimal or
# server images often don't. Each entry is soname:Debian/Ubuntu package:Fedora package.
GUI_LIBS="libX11.so.6:libx11-6:libX11 libICE.so.6:libice6:libICE libSM.so.6:libsm6:libSM libXext.so.6:libxext6:libXext libXrandr.so.2:libxrandr2:libXrandr libXi.so.6:libxi6:libXi libXcursor.so.1:libxcursor1:libXcursor libXfixes.so.3:libxfixes3:libXfixes libfontconfig.so.1:libfontconfig1:fontconfig"

# Prints the GUI_LIBS sonames the dynamic linker cannot find for this architecture, one per
# line. Prints nothing when all are present, or when there is no ldconfig to ask. TERMRIG_LDCONFIG
# overrides the ldconfig command.
missing_gui_libs() {
    _gl_ldc="${TERMRIG_LDCONFIG:-}"
    if [ -z "$_gl_ldc" ]; then
        for _gl_c in ldconfig /sbin/ldconfig /usr/sbin/ldconfig; do
            if command -v "$_gl_c" >/dev/null 2>&1; then _gl_ldc="$_gl_c"; break; fi
        done
    fi
    [ -n "$_gl_ldc" ] || return 0
    _gl_cache="$("$_gl_ldc" -p 2>/dev/null || true)"
    [ -n "$_gl_cache" ] || return 0
    case "$(uname -m)" in
        x86_64|amd64) _gl_arch=x86-64 ;;
        aarch64|arm64) _gl_arch=AArch64 ;;
        *) _gl_arch="" ;;
    esac
    for _gl_e in $GUI_LIBS; do
        _gl_so="${_gl_e%%:*}"
        printf '%s\n' "$_gl_cache" | awk -v so="$_gl_so" -v arch="$_gl_arch" \
            '$1 == so && (arch == "" || index($0, arch)) { found = 1 } END { exit !found }' \
            || printf '%s\n' "$_gl_so"
    done
}

# Installs any missing GUI_LIBS with apt-get or dnf (through sudo, which may ask for a password, unless
# this is root), then checks again. When some are still missing (no supported package manager, no
# sudo, or the install failed) it prints the command to run by hand: "error" then exits, "warning"
# returns. TERMRIG_NO_DEPS=1 skips the install and only reports.
# Usage: ensure_gui_libs error|warning
ensure_gui_libs() {
    echo "Checking the desktop libraries Termrig needs..."
    _gl_missing="$(missing_gui_libs | tr '\n' ' ')"
    _gl_missing="${_gl_missing% }"
    [ -n "$_gl_missing" ] || return 0
    _gl_deb=""; _gl_rpm=""
    for _gl_so in $_gl_missing; do
        for _gl_e in $GUI_LIBS; do
            case "$_gl_e" in
                "$_gl_so":*) _gl_r="${_gl_e#*:}"; _gl_deb="$_gl_deb ${_gl_r%%:*}"; _gl_rpm="$_gl_rpm ${_gl_r#*:}" ;;
            esac
        done
    done
    _gl_sudo=""
    _gl_can=1
    if [ "$(id -u)" != 0 ]; then
        if command -v sudo >/dev/null 2>&1; then _gl_sudo=sudo; else _gl_can=0; fi
    fi
    [ "${TERMRIG_NO_DEPS:-0}" != 1 ] || _gl_can=0
    _gl_tried=0
    if [ "$_gl_can" = 1 ] && command -v apt-get >/dev/null 2>&1; then
        _gl_tried=1
        echo "Installing missing desktop libraries ($_gl_missing):"
        echo "  ${_gl_sudo:+$_gl_sudo }apt-get install -y$_gl_deb"
        [ -z "$_gl_sudo" ] || echo "(sudo may ask for your password.)"
        # A fresh distro often has no package lists yet; a failed update still lets install try.
        # Ask for the sudo password up front. sudo resets the environment, so the non-interactive settings
        # go through env; stdin is /dev/null so nothing can wait for an answer; the lock timeout waits out
        # unattended-upgrades; the visible output and the update timeout (with an IPv4 retry for broken
        # IPv6 inside WSL or a VPN) keep a slow mirror from looking like a hang.
        [ -z "$_gl_sudo" ] || $_gl_sudo -v || true
        _gl_to=""
        if command -v timeout >/dev/null 2>&1 && timeout --foreground 5 true >/dev/null 2>&1; then _gl_to="timeout --foreground 300"; fi
        echo "Updating the package lists (apt-get update)..."
        $_gl_to $_gl_sudo env DEBIAN_FRONTEND=noninteractive NEEDRESTART_MODE=a NEEDRESTART_SUSPEND=1 apt-get -o DPkg::Lock::Timeout=300 update </dev/null \
            || $_gl_to $_gl_sudo env DEBIAN_FRONTEND=noninteractive NEEDRESTART_MODE=a NEEDRESTART_SUSPEND=1 apt-get -o DPkg::Lock::Timeout=300 -o Acquire::ForceIPv4=true update </dev/null \
            || true
        # shellcheck disable=SC2086 # the package list is split on purpose
        $_gl_sudo env DEBIAN_FRONTEND=noninteractive NEEDRESTART_MODE=a NEEDRESTART_SUSPEND=1 apt-get -o DPkg::Lock::Timeout=300 -o Dpkg::Options::=--force-confdef -o Dpkg::Options::=--force-confold install -y $_gl_deb </dev/null || true
    elif [ "$_gl_can" = 1 ] && command -v dnf >/dev/null 2>&1; then
        _gl_tried=1
        echo "Installing missing desktop libraries ($_gl_missing):"
        echo "  ${_gl_sudo:+$_gl_sudo }dnf install -y$_gl_rpm"
        [ -z "$_gl_sudo" ] || echo "(sudo may ask for your password.)"
        # shellcheck disable=SC2086 # the package list is split on purpose
        $_gl_sudo dnf install -y $_gl_rpm </dev/null || true
    fi
    if [ "$_gl_tried" = 1 ]; then
        _gl_missing="$(missing_gui_libs | tr '\n' ' ')"
        _gl_missing="${_gl_missing% }"
        if [ -z "$_gl_missing" ]; then
            echo "Installed the desktop libraries."
            return 0
        fi
    fi
    if [ "$1" = error ]; then
        echo "Error: Termrig cannot open its window without these shared libraries: $_gl_missing" >&2
    else
        echo "Warning: Termrig will be installed, but it cannot open until these shared libraries are installed: $_gl_missing" >&2
    fi
    echo "Without them, starting Termrig fails with \"Unable to load shared library '...'\"." >&2
    echo "Install them with:" >&2
    if command -v apt-get >/dev/null 2>&1; then
        echo "  sudo apt install$_gl_deb" >&2
    elif command -v dnf >/dev/null 2>&1; then
        echo "  sudo dnf install$_gl_rpm" >&2
    else
        echo "  Debian/Ubuntu: sudo apt install$_gl_deb" >&2
        echo "  Fedora:        sudo dnf install$_gl_rpm" >&2
    fi
    echo "See \"Desktop libraries\" in INSTALLING_ON_LINUX.md." >&2
    if [ "$1" = error ]; then exit 1; fi
    return 0
}

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

echo "Termrig install (Linux, $VERSION, $RID)"
echo "[1/7] Checking for running Termrig processes..."
if termrig_running; then
    # kill-termrig would close this very tab, so point at another terminal instead.
    if [ "${TERM_PROGRAM:-}" = Termrig ]; then
        die "Termrig is running, and this terminal is a Termrig tab. Quit Termrig and run this from another terminal."
    fi
    die "Termrig is running. Quit every Termrig window and trig session (or run scripts/linux/kill-termrig.sh) and try again."
fi
echo "[2/7] Checking for an existing install..."
if tool_installed; then
    die "The Termrig global tool (trig) is already installed. Run scripts/linux/reinstall-tool.sh to replace it."
fi
if app_installed; then
    die "The Termrig app is already installed. Run scripts/linux/reinstall-tool.sh to replace it."
fi
ensure_gui_libs warning
echo "[3/7] Building the Termrig tool package..."
rm -rf "$PACKAGE_SOURCE" "$NUGET_CACHE"
mkdir -p "$PACKAGE_SOURCE"
dotnet pack "$PROJECT" --configuration Release --output "$PACKAGE_SOURCE"
echo "[4/7] Publishing the Termrig app ($RID)..."
publish_app
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
case ":$ORIGINAL_PATH:" in
    *":$HOME/.dotnet/tools:"*) ;;
    *)
        echo "Note: $HOME/.dotnet/tools is not on your PATH, so new shells will not find 'trig'." >&2
        echo "Add this line to ~/.bashrc (or ~/.profile):" >&2
        echo '  export PATH="$PATH:$HOME/.dotnet/tools"' >&2 ;;
esac
if [ -z "$ORIGINAL_DOTNET_ROOT" ] && [ "$(command -v dotnet)" = "$HOME/.dotnet/dotnet" ]; then
    echo "Note: .NET is installed in ~/.dotnet, so 'trig' also needs DOTNET_ROOT. Add this line to ~/.bashrc (or ~/.profile):" >&2
    echo '  export DOTNET_ROOT="$HOME/.dotnet"' >&2
fi
if [ -z "${DISPLAY:-}${WAYLAND_DISPLAY:-}" ]; then
    echo "Note: no graphical session found (DISPLAY and WAYLAND_DISPLAY are unset). Termrig opens from a desktop session." >&2
fi
echo "Done. Start Termrig with 'trig' or from the application menu."
