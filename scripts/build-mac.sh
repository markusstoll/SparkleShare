#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

export MD_APPLE_SDK_ROOT="${MD_APPLE_SDK_ROOT:-/Applications/Xcode.app}"

HOST_ARCH="$(uname -m)"
case "$HOST_ARCH" in
    arm64)  HOST_RID="osx-arm64" ;;
    x86_64) HOST_RID="osx-x64"   ;;
    *)      HOST_RID=""          ;;
esac

CONFIG="${CONFIG:-Debug}"
# Set RID=osx-arm64 or RID=osx-x64 for a specific architecture (required for
# cross-arch release builds and matching dugite git in the app bundle).
# By default the .NET macOS SDK picks the host RID.
RID="${RID:-}"
export SPARKLESHARE_MAC_RID="${RID:-$HOST_RID}"

echo "Using Xcode: $MD_APPLE_SDK_ROOT"
echo "Host arch:   $HOST_ARCH (RID: ${HOST_RID:-unknown})"
echo "Building:    config=$CONFIG rid=${RID:-<sdk-default>}"

DOTNET="${DOTNET:-/usr/local/share/dotnet/dotnet}"
[ -x "$DOTNET" ] || DOTNET="dotnet"

# Release + RID: .NET 10 macOS runs ILLink (PublishTrimmed) and requires self-contained.
# Debug can stay framework-dependent for faster iteration.
SELF_CONTAINED=false
if [ "$CONFIG" = "Release" ]; then
    SELF_CONTAINED=true
fi

if [ -n "$RID" ]; then
    "$DOTNET" build SparkleShare/Mac/SparkleShare.Mac.csproj \
        -c "$CONFIG" -r "$RID" --self-contained "$SELF_CONTAINED" "$@"
else
    "$DOTNET" build SparkleShare/Mac/SparkleShare.Mac.csproj -c "$CONFIG" "$@"
fi

EFFECTIVE_RID="${RID:-$HOST_RID}"
MAC_BIN="SparkleShare/Mac/bin/${CONFIG}/net10.0-macos"
APP_PATH=""
if [ -n "$EFFECTIVE_RID" ] && [ -f "$MAC_BIN/${EFFECTIVE_RID}/SparkleShare.app/Contents/Resources/MainMenu.nib" ]; then
    APP_PATH="$MAC_BIN/${EFFECTIVE_RID}/SparkleShare.app"
elif [ -f "$MAC_BIN/SparkleShare.app/Contents/Resources/MainMenu.nib" ]; then
    APP_PATH="$MAC_BIN/SparkleShare.app"
elif [ -n "$EFFECTIVE_RID" ] && [ -d "$MAC_BIN/${EFFECTIVE_RID}/SparkleShare.app" ]; then
    APP_PATH="$MAC_BIN/${EFFECTIVE_RID}/SparkleShare.app"
elif [ -d "$MAC_BIN/SparkleShare.app" ]; then
    APP_PATH="$MAC_BIN/SparkleShare.app"
fi

if [ -n "$APP_PATH" ] && [ -d "$APP_PATH" ]; then
    BIN="$APP_PATH/Contents/MacOS/SparkleShare"
    echo
    echo "Built bundle: $APP_PATH"
    if command -v lipo >/dev/null 2>&1 && [ -f "$BIN" ]; then
        echo -n "  Architecture(s): "
        lipo -archs "$BIN"
    fi
    if [ ! -f "$APP_PATH/Contents/Resources/MainMenu.nib" ]; then
        echo
        echo "Warning: bundle looks incomplete (missing MainMenu.nib). Prefer:"
        echo "  $MAC_BIN/SparkleShare.app"
        echo "Run a clean Release build if signing fails."
    fi
    GIT_BIN="$APP_PATH/Contents/Resources/git/bin/git"
    if [ -x "$GIT_BIN" ]; then
        echo -n "  Bundled git: "
        file "$GIT_BIN" | sed 's/.*: //'
    fi
    if [ "$CONFIG" = "Release" ]; then
        echo
        echo "Note: Sign and notarize per architecture, e.g.:"
        echo "  RID=osx-arm64 scripts/sign-pack-notarize-mac.sh  -> dist/mac/SparkleShare_*_arm64.dmg"
        echo "  RID=osx-x64 scripts/sign-pack-notarize-mac.sh    -> dist/mac/SparkleShare_*_x64.dmg"
        echo "  Or: scripts/release-mac-dmgs.sh (both DMGs)"
        echo "      Do not sign/staple bin/Release in place — the next build will fail to overwrite it."
    fi
fi
