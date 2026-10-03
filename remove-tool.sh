#!/usr/bin/env sh
set -eu

if dotnet tool uninstall --global Termrig >/dev/null 2>&1; then
    echo "Removed Termrig global tool."
else
    echo "Termrig global tool is not installed."
fi

if [ "$(uname -s)" = "Darwin" ] && [ -d "$HOME/Applications/Termrig.app" ]; then
    rm -rf "$HOME/Applications/Termrig.app"
    echo "Removed $HOME/Applications/Termrig.app."
fi
