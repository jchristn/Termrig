#!/usr/bin/env sh
set -eu

# Check first, so a failed uninstall (for example while trig is running) is reported as a failure
# instead of "not installed".
if dotnet tool list --global | awk 'tolower($1) == "termrig" { found = 1 } END { exit !found }'; then
    dotnet tool uninstall --global Termrig >/dev/null || {
        echo "Failed to uninstall the Termrig global tool. Close any running Termrig/trig processes and rerun this script." >&2
        exit 1
    }
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
