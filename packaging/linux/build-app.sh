#!/usr/bin/env sh
# Build Termrig for this machine and install it for the current user, with an
# application-menu launcher so it can be pinned to the dock.
#
# Usage: packaging/linux/build-app.sh
#
# Installs:
#   ~/.local/share/termrig/                                      the app (self-contained)
#   ~/.local/share/applications/com.jchristn.Termrig.desktop     the launcher
#   ~/.local/share/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg
#
# No sudo needed. Uninstall with remove-tool.sh, or delete those three paths.
set -eu

if [ "$(uname -s)" != "Linux" ]; then
    echo "build-app.sh: this script installs Termrig on Linux; skipping." >&2
    exit 0
fi

SCRIPT_DIR="$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)"
REPO_ROOT="$(CDPATH='' cd -- "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/src/Termrig.App/Termrig.App.csproj"
DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
APP_DIR="$DATA_HOME/termrig"
DESKTOP_DIR="$DATA_HOME/applications"
DESKTOP_FILE="$DESKTOP_DIR/com.jchristn.Termrig.desktop"
ICON_DIR="$DATA_HOME/icons/hicolor/scalable/apps"

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

case "$(uname -m)" in
    aarch64|arm64) RID="linux-arm64" ;;
    *) RID="linux-x64" ;;
esac

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJECT" | head -n 1)"
STAGE_DIR="$REPO_ROOT/artifacts/linux-app/$RID"
PUBLISH_DIR="$STAGE_DIR/publish"

if pgrep -f "$APP_DIR/Termrig" >/dev/null 2>&1; then
    echo "Termrig is running from $APP_DIR. Quit it and run this script again." >&2
    exit 1
fi

ensure_gui_libs warning

rm -rf "$STAGE_DIR"
mkdir -p "$STAGE_DIR"

echo "Publishing Termrig $VERSION ($RID)..."
dotnet publish "$PROJECT" \
    --configuration Release \
    --runtime "$RID" \
    --self-contained true \
    --output "$PUBLISH_DIR" \
    -p:PackAsTool=false

echo "Installing to $APP_DIR..."
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR"
cp -R "$PUBLISH_DIR/." "$APP_DIR/"
chmod +x "$APP_DIR/Termrig"

echo "Installing the launcher and icon..."
mkdir -p "$DESKTOP_DIR" "$ICON_DIR"
cp "$REPO_ROOT/packaging/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg" "$ICON_DIR/com.jchristn.Termrig.svg"
# TERMRIG_FOREGROUND=1 keeps a launcher start as a single process (no detach), so the
# dock tracks the window it started.
sed -e "s|^Exec=.*|Exec=env TERMRIG_FOREGROUND=1 \"$APP_DIR/Termrig\"|" \
    "$REPO_ROOT/packaging/linux/com.jchristn.Termrig.desktop" > "$DESKTOP_FILE"
chmod 644 "$DESKTOP_FILE"

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$DESKTOP_DIR" >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q "$DATA_HOME/icons/hicolor" >/dev/null 2>&1 || true
fi

echo "Installed Termrig $VERSION. Open it from your application menu (search for \"Termrig\")."
