#!/bin/sh
# Builds every installer this host can produce into installers/<version>/:
#   macOS: Termrig.app, .pkg, .dmg, and .tar.gz for osx-x64 and osx-arm64
#   Linux: .deb, .rpm, AppImage, and .tar.gz for linux-x64 and linux-arm64 (needs fpm, rpmbuild, appimagetool)
# Usage: ./build-installers.sh [version]   (default: the <Version> in src/Termrig.App/Termrig.App.csproj)
# CI runs the same Publisher channels on each OS; see .github/workflows/release.yml.
set -eu
repo=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$repo"
dotnet build src/Termrig.Publisher -c Release --nologo -v quiet
if [ "$#" -gt 0 ]; then
  dotnet run --project src/Termrig.Publisher -c Release --no-build -- --all --version "$1"
else
  dotnet run --project src/Termrig.Publisher -c Release --no-build -- --all
fi
