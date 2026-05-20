#!/usr/bin/env bash
#
# Build, sign, notarize, and staple separate Release DMGs for Apple Silicon and Intel.
# Requires sign-pack-notarize-mac.local.sh (or exported SIGNING_IDENTITY / NOTARY_KEYCHAIN_PROFILE).
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT"

export CONFIG=Release
export MD_APPLE_SDK_ROOT="${MD_APPLE_SDK_ROOT:-/Applications/Xcode.app}"

MAC_BIN="SparkleShare/Mac/bin/Release/net10.0-macos"

for RID in osx-arm64 osx-x64; do
    echo "========== $RID =========="
    # Full clean per architecture so version/plist and CPU type cannot leak across DMGs.
    rm -rf "$ROOT/$MAC_BIN"
    RID="$RID" "$SCRIPT_DIR/build-mac.sh"
    RID="$RID" "$SCRIPT_DIR/sign-pack-notarize-mac.sh"
    echo
done

echo "All release DMGs are under dist/mac/:"
ls -1 dist/mac/SparkleShare_*.dmg 2>/dev/null || true
