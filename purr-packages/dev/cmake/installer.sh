#!/usr/bin/env bash
set -euo pipefail

# this script is invoked by `purr` with a single argument: the URL of a
# platform-specific release asset.  the asset is downloaded, unpacked, and the
# main executable is copied into PURR_BIN_DIR.

ASSET_URL="$1"
TMP=$(mktemp -d)

curl -L "$ASSET_URL" -o "$TMP/asset.zip"
mkdir -p "$TMP/ex"
unzip -q "$TMP/asset.zip" -d "$TMP/ex"

# look for the cmake binary (the dirname is unpredictable inside the archive)
MAIN=$(find "$TMP/ex" -type f -name 'cmake' -print -quit)
if [ -z "$MAIN" ]; then
    echo "error: cmake executable not found in archive" >&2
    exit 1
fi

install -d "$PURR_BIN_DIR"
cp "$MAIN" "$PURR_BIN_DIR/cmake"
chmod +x "$PURR_BIN_DIR/cmake"
echo "Installed cmake to $PURR_BIN_DIR"
