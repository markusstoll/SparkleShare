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
# Optional: set RID=osx-x64 (or osx-arm64) to force a specific architecture.
# By default we let the .NET macOS SDK pick the host RID, which avoids an
# extra NuGet restore for cross-arch runtime packages.
RID="${RID:-}"

echo "Using Xcode: $MD_APPLE_SDK_ROOT"
echo "Host arch:   $HOST_ARCH (RID: ${HOST_RID:-unknown})"
echo "Building:    config=$CONFIG rid=${RID:-<sdk-default>}"

DOTNET="${DOTNET:-/usr/local/share/dotnet/dotnet}"
[ -x "$DOTNET" ] || DOTNET="dotnet"

if [ -n "$RID" ]; then
    "$DOTNET" build SparkleShare/Mac/SparkleShare.Mac.csproj \
        -c "$CONFIG" -r "$RID" --self-contained false "$@"
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
    if [ "$CONFIG" = "Release" ]; then
        echo
        echo "Note: Run scripts/sign-pack-notarize-mac.sh to build dist/mac/SparkleShare_VERSION.dmg."
        echo "      Do not sign/staple bin/Release in place — the next build will fail to overwrite it."
    fi
fi
