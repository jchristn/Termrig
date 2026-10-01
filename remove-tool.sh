#!/usr/bin/env sh
set -eu

if dotnet tool uninstall --global Termrig >/dev/null 2>&1; then
    echo "Removed Termrig global tool."
else
    echo "Termrig global tool is not installed."
fi
