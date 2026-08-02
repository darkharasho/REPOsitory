#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")"

VERSION="$(python3 -c "import json; print(json.load(open('manifest.json'))['version_number'])")"
DLL="bin/Release/netstandard2.1/PurchaseDiag.dll"
BUILDS_DIR="$(cd ../.. && pwd)/builds"
mkdir -p "$BUILDS_DIR"
OUT="$BUILDS_DIR/PurchaseDiag-${VERSION}.zip"
# Deploy into the profile that has the coil mod.
R2_PROFILE="${R2_PROFILE:-0.4.0}"
R2_PLUGINS="$HOME/.config/r2modmanPlus-local/REPO/profiles/$R2_PROFILE/BepInEx/plugins/PurchaseDiag"

# Resolve game directory for game DLLs (Assembly-CSharp, UnityEngine, Photon).
if [ -z "${GAME_DIR:-}" ]; then
    for candidate in \
        "/var/mnt/data/SteamLibrary/steamapps/common/REPO" \
        "$HOME/.steam/steam/steamapps/common/REPO" \
        "$HOME/.local/share/Steam/steamapps/common/REPO"
    do
        if [ -d "$candidate" ]; then
            GAME_DIR="$candidate"
            break
        fi
    done
fi

if [ -z "${GAME_DIR:-}" ]; then
    echo "ERROR: Could not find R.E.P.O. install. Set GAME_DIR manually:"
    echo "  GAME_DIR=\"/path/to/REPO\" ./package.sh"
    exit 1
fi

echo "Using game dir: $GAME_DIR"

# Build
dotnet build PurchaseDiag.csproj --configuration Release /p:GameDir="$GAME_DIR"

# Deploy to r2modman profile for local testing
mkdir -p "$R2_PLUGINS"
cp "$DLL" "$R2_PLUGINS/"
echo "Deployed to r2modman profile: $R2_PROFILE"

# Package (icon optional for a throwaway diagnostic)
rm -f "$OUT"
if [ -f "icon.png" ]; then
    zip -j "$OUT" manifest.json icon.png README.md "$DLL"
    echo "Packaged: $OUT"
else
    echo "No icon.png — skipped Thunderstore zip (DLL deployed to profile is enough for local testing)."
fi
