#!/usr/bin/env sh
set -eu

if dotnet tool uninstall --global Termrig >/dev/null 2>&1; then
    echo "Removed Termrig global tool."
else
    echo "Termrig global tool is not installed."
fi

if [ "$(uname -s)" = "Darwin" ]; then
    for APP in "${TERMRIG_SYSTEM_APPLICATIONS:-/Applications}/Termrig.app" "$HOME/Applications/Termrig.app"; do
        if [ -d "$APP" ]; then
            rm -rf "$APP"
            echo "Removed $APP."
        fi
    done
fi

if [ "$(uname -s)" = "Linux" ]; then
    DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
    for ITEM in \
        "$DATA_HOME/termrig" \
        "$DATA_HOME/applications/com.jchristn.Termrig.desktop" \
        "$DATA_HOME/icons/hicolor/scalable/apps/com.jchristn.Termrig.svg"; do
        if [ -e "$ITEM" ]; then
            rm -rf "$ITEM"
            echo "Removed $ITEM."
        fi
    done
fi
