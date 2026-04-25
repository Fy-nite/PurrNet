#!/usr/bin/env bash
set -euo pipefail

# simple raylib installer; packages the raylib static/dynamic library or
# executable shipped in release assets.  adjust selection ("raylib") as
# needed for your platform or build.

ASSET_URL="$1"
TMP=$(mktemp -d)

curl -L "$ASSET_URL" -o "$TMP/asset.zip"
mkdir -p "$TMP/ex"
unzip -q "$TMP/asset.zip" -d "$TMP/ex"

MAIN=$(find "$TMP/ex" -type f -name 'raylib' -print -quit)
if [ -z "$MAIN" ]; then
    echo "error: raylib binary not found in archive" >&2
    exit 1
fi

install -d "$PURR_BIN_DIR"
cp "$MAIN" "$PURR_BIN_DIR/raylib"
chmod +x "$PURR_BIN_DIR/raylib"
echo "Installed raylib to $PURR_BIN_DIR"
