#!/bin/sh
# Shared helpers for the scripts in scripts/wsl and scripts/wsl2. Source it; don't run it.
# Set WSL_FLAVOR to "wsl" (WSL 1) or "wsl2" before sourcing. The helpers fix the usual reasons
# scripts/linux fails under WSL:
#   - WSL appends the Windows PATH, so names such as claude, codex, gemini, node, npm and code can
#     resolve to Windows shims under /mnt/c. Windows entries are dropped; set WSL_KEEP_WINDOWS_PATH=1
#     to keep them.
#   - ~/.dotnet and ~/.dotnet/tools (user-local .NET installs and global tools) are often not on PATH.
#   - A repository cloned with Windows Git (core.autocrlf=true) has CRLF scripts that fail with
#     "bash\r: No such file or directory".
#   - A repository on /mnt/<drive> (DrvFs) has no executable bits, is slow, and can be locked by
#     Windows processes.
#   - systemd and desktop autostart entries do nothing at Windows login, so start-at-login uses a
#     hidden launcher in the Windows Startup folder that runs the command through wsl.exe.
#   - Under WSL 2 NAT networking, 127.0.0.1 inside the distro is not the Windows host.

WSL_ORIGINAL_PATH="$PATH"
# shellcheck disable=SC2034 # read by the scripts that source this file
WSL_ORIGINAL_DOTNET_ROOT="${DOTNET_ROOT:-}"

# Prints 1 for WSL 1, 2 for WSL 2, or 0 when not running under WSL. A Microsoft kernel alone is not
# enough: Docker Desktop containers on the WSL 2 backend have one too, but no WSL interop.
wsl_version() {
    if [ -z "${WSL_DISTRO_NAME:-}" ] && [ ! -d /run/WSL ] && ! ls /proc/sys/fs/binfmt_misc/WSLInterop* >/dev/null 2>&1; then
        echo 0; return 0
    fi
    _wsl_rel="$(uname -r 2>/dev/null || true)"
    case "$_wsl_rel" in
        *microsoft-standard*|*WSL2*) echo 2; return 0 ;;
        *Microsoft*) echo 1; return 0 ;;
    esac
    if grep -qi 'wsl2\|microsoft-standard' /proc/version 2>/dev/null; then echo 2
    elif grep -qi microsoft /proc/version 2>/dev/null; then echo 1
    else echo 0
    fi
}

wsl_log() { printf '%s\n' "$*" >&2; }
wsl_die() { printf 'Error: %s\n' "$*" >&2; exit 1; }

# Fails outside WSL, and when the WSL version doesn't match this directory (scripts/wsl is WSL 1,
# scripts/wsl2 is WSL 2). WSL_SKIP_CHECK=1 skips both checks.
wsl_require() {
    [ "${WSL_SKIP_CHECK:-0}" = 1 ] && return 0
    _wsl_v="$(wsl_version)"
    [ "$_wsl_v" != 0 ] || wsl_die "This script is for WSL. Use scripts/linux, scripts/macos or scripts\\windows instead."
    case "${WSL_FLAVOR:-wsl2}" in
        wsl) [ "$_wsl_v" = 1 ] || wsl_die "This distro runs WSL 2. Use the same script in scripts/wsl2 instead." ;;
        wsl2) [ "$_wsl_v" = 2 ] || wsl_die "This distro runs WSL 1. Use the same script in scripts/wsl instead." ;;
    esac
}

# Drops Windows (/mnt/<drive>/...) PATH entries and adds user-local .NET and tool directories.
wsl_sanitize_path() {
    if [ "${WSL_KEEP_WINDOWS_PATH:-0}" != 1 ]; then
        _wsl_new=""
        _wsl_ifs="$IFS"; IFS=:
        for _wsl_p in $PATH; do
            case "$_wsl_p" in
                /mnt/[a-zA-Z]/*|/mnt/[a-zA-Z]) continue ;;
            esac
            _wsl_new="${_wsl_new:+$_wsl_new:}$_wsl_p"
        done
        IFS="$_wsl_ifs"
        PATH="$_wsl_new"
    fi
    for _wsl_p in "$HOME/.local/bin" "$HOME/.dotnet/tools"; do
        [ -d "$_wsl_p" ] || continue
        case ":$PATH:" in *":$_wsl_p:"*) ;; *) PATH="$PATH:$_wsl_p" ;; esac
    done
    # A user-local .NET (dotnet-install.sh) is used only when no other Linux dotnet is on PATH.
    if ! command -v dotnet >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
        PATH="$HOME/.dotnet:$PATH"
        if [ -z "${DOTNET_ROOT:-}" ]; then DOTNET_ROOT="$HOME/.dotnet"; export DOTNET_ROOT; fi
    fi
    export PATH
}

# Fails with a helpful message when a Linux command is missing (after wsl_sanitize_path, Windows
# copies no longer count). Usage: wsl_require_cmd <name> [install hint]
wsl_require_cmd() {
    command -v "$1" >/dev/null 2>&1 && return 0
    _wsl_hint="${2:-Install it inside this WSL distro.}"
    if PATH="$WSL_ORIGINAL_PATH" command -v "$1" >/dev/null 2>&1 || PATH="$WSL_ORIGINAL_PATH" command -v "$1.exe" >/dev/null 2>&1; then
        wsl_die "'$1' is only installed on the Windows side. $_wsl_hint (Or run the scripts/windows version from Windows.)"
    fi
    wsl_die "'$1' was not found. $_wsl_hint"
}

# Shared libraries Termrig loads when it opens its window: Avalonia's X11 backend (libX11, libICE,
# libSM, libXext, libXrandr, libXi, libXcursor, libXfixes) and SkiaSharp's fontconfig. Building does
# not need them, but without them Termrig stops at startup with "Unable to load shared library
# 'libICE.so.6'" (or another name below). Desktop installs have them; WSL distros and minimal or
# server images often don't. Each entry is soname:Debian/Ubuntu package:Fedora package.
WSL_GUI_LIBS="libX11.so.6:libx11-6:libX11 libICE.so.6:libice6:libICE libSM.so.6:libsm6:libSM libXext.so.6:libxext6:libXext libXrandr.so.2:libxrandr2:libXrandr libXi.so.6:libxi6:libXi libXcursor.so.1:libxcursor1:libXcursor libXfixes.so.3:libxfixes3:libXfixes libfontconfig.so.1:libfontconfig1:fontconfig"

# Prints the WSL_GUI_LIBS sonames the dynamic linker cannot find for this architecture, one per
# line. Prints nothing when all are present, or when there is no ldconfig to ask. TERMRIG_LDCONFIG
# overrides the ldconfig command.
wsl_missing_gui_libs() {
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
    for _gl_e in $WSL_GUI_LIBS; do
        _gl_so="${_gl_e%%:*}"
        printf '%s\n' "$_gl_cache" | awk -v so="$_gl_so" -v arch="$_gl_arch" \
            '$1 == so && (arch == "" || index($0, arch)) { found = 1 } END { exit !found }' \
            || printf '%s\n' "$_gl_so"
    done
}

# Installs any missing WSL_GUI_LIBS with apt-get or dnf (through sudo, which may ask for a password, unless
# this is root), then checks again. When some are still missing (no supported package manager, no
# sudo, or the install failed) it prints the command to run by hand: "error" then exits, "warning"
# returns. TERMRIG_NO_DEPS=1 skips the install and only reports.
# Usage: wsl_ensure_gui_libs error|warning
wsl_ensure_gui_libs() {
    echo "Checking the desktop libraries Termrig needs..."
    _gl_missing="$(wsl_missing_gui_libs | tr '\n' ' ')"
    _gl_missing="${_gl_missing% }"
    [ -n "$_gl_missing" ] || return 0
    _gl_deb=""; _gl_rpm=""
    for _gl_so in $_gl_missing; do
        for _gl_e in $WSL_GUI_LIBS; do
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
        echo "Installing missing desktop libraries inside this WSL distro ($_gl_missing):"
        echo "  ${_gl_sudo:+$_gl_sudo }apt-get install -y$_gl_deb"
        [ -z "$_gl_sudo" ] || echo "(sudo may ask for your password.)"
        # A fresh distro often has no package lists yet; a failed update still lets install try.
        $_gl_sudo env DEBIAN_FRONTEND=noninteractive apt-get update -qq || true
        # shellcheck disable=SC2086 # the package list is split on purpose
        $_gl_sudo env DEBIAN_FRONTEND=noninteractive apt-get install -y $_gl_deb || true
    elif [ "$_gl_can" = 1 ] && command -v dnf >/dev/null 2>&1; then
        _gl_tried=1
        echo "Installing missing desktop libraries ($_gl_missing):"
        echo "  ${_gl_sudo:+$_gl_sudo }dnf install -y$_gl_rpm"
        [ -z "$_gl_sudo" ] || echo "(sudo may ask for your password.)"
        # shellcheck disable=SC2086 # the package list is split on purpose
        $_gl_sudo dnf install -y $_gl_rpm || true
    fi
    if [ "$_gl_tried" = 1 ]; then
        _gl_missing="$(wsl_missing_gui_libs | tr '\n' ' ')"
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
    echo "Install them inside this WSL distro with:" >&2
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

# Warns about DrvFs checkouts and fails on CRLF scripts. Usage: wsl_check_repo <repo root>
wsl_check_repo() {
    case "$1" in
        /mnt/[a-zA-Z]/*)
            wsl_log "Warning: $1 is on a Windows drive (DrvFs). Builds are much slower there and"
            wsl_log "file locks from Windows processes can break them. Clone into the Linux file"
            wsl_log "system (for example ~/src) for best results." ;;
    esac
    _wsl_cr="$(printf '\r')"
    _wsl_bad="$(find "$1/scripts" -name '*.sh' -type f -exec grep -l "$_wsl_cr" {} + 2>/dev/null | head -n 5 || true)"
    if [ -n "$_wsl_bad" ]; then
        wsl_log "These scripts have Windows (CRLF) line endings and will not run under WSL:"
        wsl_log "$_wsl_bad"
        wsl_log "Commit or stash your changes first (the commands below discard uncommitted edits), then run:"
        wsl_die "git config core.autocrlf input && git rm -r --cached -q . && git reset --hard"
    fi
}

# Runs a script with the interpreter from its shebang, so missing executable bits (DrvFs) don't matter.
# Usage: wsl_run <script> [args...]
wsl_run() {
    _wsl_script="$1"; shift
    [ -f "$_wsl_script" ] || wsl_die "Missing script: $_wsl_script"
    case "$(head -n 1 "$_wsl_script")" in
        *bash*) exec bash "$_wsl_script" "$@" ;;
        *) exec sh "$_wsl_script" "$@" ;;
    esac
}

# True when systemd is PID 1 and a user manager is reachable (WSL 2 with [boot] systemd=true).
wsl_has_systemd() {
    [ "$(ps -p 1 -o comm= 2>/dev/null)" = systemd ] && command -v systemctl >/dev/null 2>&1 \
        && systemctl --user show-environment >/dev/null 2>&1
}

# Finds a Windows executable (cmd.exe, wsl.exe, ...) even after the PATH is sanitized.
wsl_win_exe() {
    _wsl_found="$(PATH="$WSL_ORIGINAL_PATH" command -v "$1" 2>/dev/null || true)"
    if [ -z "$_wsl_found" ]; then
        for _wsl_d in /mnt/c/Windows/System32 /mnt/c/Windows; do
            [ -x "$_wsl_d/$1" ] && { _wsl_found="$_wsl_d/$1"; break; }
        done
    fi
    [ -n "$_wsl_found" ] || wsl_die "Cannot find $1. Windows interop must be enabled ([interop] enabled=true in /etc/wsl.conf)."
    printf '%s\n' "$_wsl_found"
}

# Prints the Windows host's address as seen from WSL 2 NAT networking.
wsl_windows_host() {
    _wsl_gw="$(ip route show default 2>/dev/null | awk '/default/ {print $3; exit}' || true)"
    [ -n "$_wsl_gw" ] || _wsl_gw="$(awk '/^nameserver/ {print $2; exit}' /etc/resolv.conf 2>/dev/null || true)"
    printf '%s\n' "$_wsl_gw"
}

# Under WSL 2 NAT networking, rewrites a 127.0.0.1/localhost URL to the Windows host when nothing
# answers inside the distro but the Windows host does (a server running on Windows). Leaves the URL
# alone on WSL 1, with mirrored networking, when curl is missing, or when WSL_NO_HOST_REWRITE=1.
# Usage: url="$(wsl_resolve_local_url "$url")"
wsl_resolve_local_url() {
    _wsl_url="$1"
    [ "${WSL_NO_HOST_REWRITE:-0}" = 1 ] && { printf '%s\n' "$_wsl_url"; return 0; }
    [ "$(wsl_version)" = 2 ] || { printf '%s\n' "$_wsl_url"; return 0; }
    command -v curl >/dev/null 2>&1 || { printf '%s\n' "$_wsl_url"; return 0; }
    case "$_wsl_url" in
        *://127.0.0.1|*://127.0.0.1:*|*://127.0.0.1/*|*://localhost|*://localhost:*|*://localhost/*) ;;
        *) printf '%s\n' "$_wsl_url"; return 0 ;;
    esac
    if curl -s -o /dev/null --max-time 2 "$_wsl_url" 2>/dev/null; then printf '%s\n' "$_wsl_url"; return 0; fi
    _wsl_host="$(wsl_windows_host)"
    [ -n "$_wsl_host" ] || { printf '%s\n' "$_wsl_url"; return 0; }
    _wsl_alt="$(printf '%s\n' "$_wsl_url" | sed -e "s#://127\.0\.0\.1#://$_wsl_host#" -e "s#://localhost#://$_wsl_host#")"
    if curl -s -o /dev/null --max-time 2 "$_wsl_alt" 2>/dev/null; then
        wsl_log "Nothing answers at $_wsl_url inside WSL, but the Windows host does; using $_wsl_alt."
        wsl_log "(Set WSL_NO_HOST_REWRITE=1 to keep the original, or enable networkingMode=mirrored in .wslconfig.)"
        printf '%s\n' "$_wsl_alt"; return 0
    fi
    printf '%s\n' "$_wsl_url"
}

# Prints the launcher file name for <name> in this distro, so several distros can register the same
# command without replacing each other's launcher. Usage: wsl_launcher_file <name>
wsl_launcher_file() {
    printf '%s-wsl-%s.vbs\n' "$1" "$(printf '%s' "${WSL_DISTRO_NAME:-default}" | tr -c 'A-Za-z0-9._-' '_')"
}

# Removes a launcher written by an older version of these helpers (<name>-wsl.vbs, one name for every
# distro) when it belongs to this distro. Returns 0 when one was removed. Usage: wsl_startup_remove_legacy <dir> <name>
wsl_startup_remove_legacy() {
    _wsl_legacy="$1/$2-wsl.vbs"
    [ -f "$_wsl_legacy" ] || return 1
    grep -qF -- "-d \"\"${WSL_DISTRO_NAME:-}\"\"" "$_wsl_legacy" 2>/dev/null || return 1
    rm -f "$_wsl_legacy"
    wsl_log "Removed the older Windows Startup launcher: $_wsl_legacy"
}

# Prints the Windows Startup folder as a WSL path.
wsl_startup_dir() {
    _wsl_cmd="$(wsl_win_exe cmd.exe)"
    _wsl_appdata="$(cd /mnt/c 2>/dev/null || true; "$_wsl_cmd" /c 'echo %APPDATA%' 2>/dev/null | tr -d '\r' || true)"
    [ -n "$_wsl_appdata" ] || wsl_die "Could not read %APPDATA% from Windows."
    printf '%s/Microsoft/Windows/Start Menu/Programs/Startup\n' "$(wslpath -u "$_wsl_appdata")"
}

# Registers a command to start, hidden, at Windows login, inside this distro. The launcher keeps
# wsl.exe attached so the distro stays up while the command runs. No admin rights needed.
# Usage: wsl_startup_install <name> <command> [args...]   (name: letters, digits, dashes)
wsl_startup_install() {
    _wsl_name="$1"; shift
    [ -n "${WSL_DISTRO_NAME:-}" ] || wsl_die "WSL_DISTRO_NAME is not set; run this from a WSL shell."
    _wsl_dir="$(wsl_startup_dir)" || exit 1
    mkdir -p "$_wsl_dir"
    wsl_startup_remove_legacy "$_wsl_dir" "$_wsl_name" || true
    _wsl_args=""
    for _wsl_a in "$@"; do
        _wsl_q="$(printf '%s' "$_wsl_a" | sed "s/'/'\\\\''/g")"
        _wsl_args="$_wsl_args '$_wsl_q'"
    done
    # VBScript doubles quotes inside string literals.
    _wsl_line="wsl.exe -d \"\"$WSL_DISTRO_NAME\"\" --cd ~ --exec /bin/sh -lc \"\"exec$_wsl_args\"\""
    printf 'CreateObject("WScript.Shell").Run "%s", 0, False\r\n' "$_wsl_line" > "$_wsl_dir/$(wsl_launcher_file "$_wsl_name")"
    wsl_log "Installed the Windows Startup launcher: $_wsl_dir/$(wsl_launcher_file "$_wsl_name")"
}

# Usage: wsl_startup_remove <name>   (returns 1 when nothing was registered; exits when the Startup
# folder cannot be found, rather than reporting "nothing registered")
wsl_startup_remove() {
    _wsl_dir="$(wsl_startup_dir)" || exit 1
    _wsl_file="$_wsl_dir/$(wsl_launcher_file "$1")"
    _wsl_removed=1
    if wsl_startup_remove_legacy "$_wsl_dir" "$1"; then _wsl_removed=0; fi
    [ -f "$_wsl_file" ] || return "$_wsl_removed"
    rm -f "$_wsl_file"
    wsl_log "Removed the Windows Startup launcher: $_wsl_file"
}

# Runs a launcher installed by wsl_startup_install now, the same way Windows runs it at login, so the
# command keeps running after this terminal closes. Usage: wsl_startup_run <name>
wsl_startup_run() {
    _wsl_dir="$(wsl_startup_dir)" || exit 1
    _wsl_file="$_wsl_dir/$(wsl_launcher_file "$1")"
    [ -f "$_wsl_file" ] || wsl_die "No Windows Startup launcher named $1 is installed."
    _wsl_wscript="$(wsl_win_exe wscript.exe)"
    _wsl_winfile="$(wslpath -w "$_wsl_file")"
    (cd /mnt/c 2>/dev/null || true; "$_wsl_wscript" "$_wsl_winfile")
}

# True when a launcher installed by wsl_startup_install exists. Usage: wsl_startup_exists <name>
wsl_startup_exists() {
    _wsl_dir="$( (wsl_startup_dir) 2>/dev/null || true)"
    [ -n "$_wsl_dir" ] || return 1
    [ -f "$_wsl_dir/$(wsl_launcher_file "$1")" ] && return 0
    grep -qF -- "-d \"\"${WSL_DISTRO_NAME:-}\"\"" "$_wsl_dir/$1-wsl.vbs" 2>/dev/null
}
