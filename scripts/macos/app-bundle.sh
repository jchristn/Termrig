#!/usr/bin/env bash
# Termrig.app helpers for the scripts in scripts/macos. Source it; don't run it. It only defines
# functions. The caller defines die() and sets REPO_ROOT, PROJECT and VERSION.
#
# Termrig.app is installed in /Applications when it is writable, otherwise in ~/Applications. A copy
# left in the other location is removed, so Spotlight and Finder only see the copy just installed.
# The bundle is self-contained, so it runs without the .NET runtime installed.
# TERMRIG_SYSTEM_APPLICATIONS and TERMRIG_LSREGISTER override /Applications and lsregister (tests).

# Sets SYSTEM_APPS, USER_APPS, INSTALL_DIR and LSREGISTER.
macos_app_paths() {
    SYSTEM_APPS="${TERMRIG_SYSTEM_APPLICATIONS:-/Applications}"
    USER_APPS="$HOME/Applications"
    if [ -w "$SYSTEM_APPS" ]; then
        INSTALL_DIR="$SYSTEM_APPS"
    else
        INSTALL_DIR="$USER_APPS"
    fi
    LSREGISTER="${TERMRIG_LSREGISTER:-/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister}"
}

# Sets RID, STAGE_DIR and STAGED_APP for this Mac's architecture.
macos_rid() {
    case "$(uname -m)" in
        arm64) RID=osx-arm64 ;;
        x86_64) RID=osx-x64 ;;
        *) die "Unsupported macOS architecture: $(uname -m)." ;;
    esac
    STAGE_DIR="$REPO_ROOT/artifacts/macos-app/$RID"
    STAGED_APP="$STAGE_DIR/Termrig.app"
}

# Fails unless the tools that assemble and sign the bundle are present.
macos_require_app_tools() {
    local tool
    for tool in sips iconutil plutil codesign; do
        command -v "$tool" >/dev/null 2>&1 \
            || die "$tool was not found on PATH. It ships with macOS (codesign and iconutil may need the Xcode command line tools: xcode-select --install)."
    done
}

# True when Termrig.app exists in either standard location.
macos_app_installed() {
    [ -d "$SYSTEM_APPS/Termrig.app" ] || [ -d "$USER_APPS/Termrig.app" ]
}

# Publishes, assembles and ad-hoc signs Termrig.app in the staging directory. Nothing installed
# changes, so a failed build or signature leaves the current install in place.
build_macos_app() {
    local publish="$STAGE_DIR/publish" iconset="$STAGE_DIR/Termrig.iconset" icon size double
    icon="$REPO_ROOT/packaging/icons/termrig-macos.png"
    [ -f "$icon" ] || die "Missing $icon."
    [ -f "$REPO_ROOT/packaging/macos/Info.plist" ] || die "Missing $REPO_ROOT/packaging/macos/Info.plist."
    rm -rf "$STAGE_DIR"
    mkdir -p "$STAGE_DIR"
    dotnet publish "$PROJECT" --configuration Release --runtime "$RID" --self-contained true \
        --output "$publish" -p:PackAsTool=false
    [ -f "$publish/Termrig" ] || die "dotnet publish did not produce $publish/Termrig."
    mkdir -p "$iconset"
    for size in 16 32 128 256 512; do
        double=$((size * 2))
        sips -z "$size" "$size" "$icon" --out "$iconset/icon_${size}x${size}.png" >/dev/null
        sips -z "$double" "$double" "$icon" --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
    done
    mkdir -p "$STAGED_APP/Contents/MacOS" "$STAGED_APP/Contents/Resources"
    cp -R "$publish/." "$STAGED_APP/Contents/MacOS/"
    iconutil --convert icns "$iconset" --output "$STAGED_APP/Contents/Resources/Termrig.icns"
    cp "$REPO_ROOT/packaging/macos/Info.plist" "$STAGED_APP/Contents/Info.plist"
    plutil -replace CFBundleShortVersionString -string "$VERSION" "$STAGED_APP/Contents/Info.plist"
    plutil -replace CFBundleVersion -string "$VERSION" "$STAGED_APP/Contents/Info.plist"
    chmod +x "$STAGED_APP/Contents/MacOS/Termrig"
    codesign --force --deep --sign - "$STAGED_APP" || die "Signing Termrig.app failed; the installed app was left unchanged."
}

# Replaces $INSTALL_DIR/Termrig.app with the staged bundle, registers it with Launch Services and
# removes a copy left in the other standard location.
install_macos_app() {
    local installed other
    mkdir -p "$INSTALL_DIR"
    rm -rf "$INSTALL_DIR/Termrig.app"
    cp -R "$STAGED_APP" "$INSTALL_DIR/Termrig.app"
    "$LSREGISTER" -f "$INSTALL_DIR/Termrig.app" >/dev/null 2>&1 || true
    echo "Installed $INSTALL_DIR/Termrig.app"
    installed="$(cd "$INSTALL_DIR/Termrig.app" && pwd -P)"
    for other in "$SYSTEM_APPS/Termrig.app" "$USER_APPS/Termrig.app"; do
        [ -d "$other" ] || continue
        [ "$(cd "$other" && pwd -P)" != "$installed" ] || continue
        if rm -rf "$other" 2>/dev/null; then
            "$LSREGISTER" -u "$other" >/dev/null 2>&1 || true
            echo "Removed stale copy $other"
        else
            echo "Warning: could not remove stale copy $other; Spotlight may open it instead." >&2
        fi
    done
}

# Deletes Termrig.app from both standard locations and unregisters it. Prints what it removed.
remove_macos_app() {
    local app
    for app in "$SYSTEM_APPS/Termrig.app" "$USER_APPS/Termrig.app"; do
        [ -d "$app" ] || continue
        rm -rf "$app" || die "Could not remove $app. Delete it in Finder (it may need an administrator)."
        "$LSREGISTER" -u "$app" >/dev/null 2>&1 || true
        echo "Removed $app"
    done
}
