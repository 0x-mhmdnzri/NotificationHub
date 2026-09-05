#!/usr/bin/env bash
# Install local IRANSans FaNum fonts from Archive.zip (no CDN).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ZIP="${1:-}"
DEST="$ROOT/public/fonts/iransans"
mkdir -p "$DEST"
if [[ -z "$ZIP" ]]; then
  echo "Usage: $0 /path/to/Archive.zip"
  exit 1
fi
TMP="$(mktemp -d)"
unzip -o "$ZIP" -d "$TMP"
cp "$TMP/iransansWeb(FaNum)_UltraLight.woff2" "$DEST/IRANSans-FaNum_UltraLight.woff2"
cp "$TMP/iransansWeb(FaNum)_Light.woff2"      "$DEST/IRANSans-FaNum_Light.woff2"
cp "$TMP/iransansWeb(FaNum).woff2"            "$DEST/IRANSans-FaNum_Regular.woff2"
cp "$TMP/iransansWeb(FaNum)_Medium.woff2"     "$DEST/IRANSans-FaNum_Medium.woff2"
cp "$TMP/iransansWeb(FaNum)_Bold.woff2"       "$DEST/IRANSans-FaNum_Bold.woff2"
cp "$TMP/iransansWeb(FaNum)_UltraLight.woff"  "$DEST/IRANSans-FaNum_UltraLight.woff"
cp "$TMP/iransansWeb(FaNum)_Light.woff"       "$DEST/IRANSans-FaNum_Light.woff"
cp "$TMP/iransansWeb(FaNum).woff"             "$DEST/IRANSans-FaNum_Regular.woff"
cp "$TMP/iransansWeb(FaNum)_Medium.woff"      "$DEST/IRANSans-FaNum_Medium.woff"
cp "$TMP/iransansWeb(FaNum)_Bold.woff"        "$DEST/IRANSans-FaNum_Bold.woff"
rm -rf "$TMP"
echo "Installed to $DEST"
ls -la "$DEST"
