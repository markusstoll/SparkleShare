#!/usr/bin/env bash
#
# Sign, pack as a drag-to-Applications DMG, notarize, and staple for macOS distribution.
#
# One-time notary credentials (app-specific password from appleid.apple.com):
#   xcrun notarytool store-credentials "YOUR_PROFILE_NAME" \
#     --apple-id "you@example.com" \
#     --team-id "YOUR_TEAM_ID" \
#     --password "xxxx-xxxx-xxxx-xxxx"
#
# Build first (pick architecture — separate DMGs for Apple Silicon and Intel):
#   CONFIG=Release RID=osx-arm64 scripts/build-mac.sh
#   RID=osx-arm64 scripts/sign-pack-notarize-mac.sh
#   CONFIG=Release RID=osx-x64 scripts/build-mac.sh
#   RID=osx-x64 scripts/sign-pack-notarize-mac.sh
#
# Or both in one go (after configuring credentials):
#   scripts/release-mac-dmgs.sh
#
# Credentials (pick one):
#   1. Copy scripts/sign-pack-notarize-mac.local.sh.example to
#      scripts/sign-pack-notarize-mac.local.sh and edit (gitignored)
#   2. Export SIGNING_IDENTITY and NOTARY_KEYCHAIN_PROFILE for this shell
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
LOCAL_CONFIG="$SCRIPT_DIR/sign-pack-notarize-mac.local.sh"

# Defaults; overridden by sign-pack-notarize-mac.local.sh (if present).
SIGNING_IDENTITY="${SIGNING_IDENTITY:-Developer ID Application: Your Name (TEAMID)}"
NOTARY_KEYCHAIN_PROFILE="${NOTARY_KEYCHAIN_PROFILE:-YOUR_NOTARY_PROFILE}"

if [ -f "$LOCAL_CONFIG" ]; then
    # shellcheck source=/dev/null
    . "$LOCAL_CONFIG"
fi

ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
MAC_DIR="$ROOT/SparkleShare/Mac"
ENTITLEMENTS="$MAC_DIR/SparkleShare.entitlements"
CSPROJ="$MAC_DIR/SparkleShare.Mac.csproj"

CONFIG="${CONFIG:-Release}"
RID="${RID:-}"
DIST_DIR="$ROOT/dist/mac"

HOST_ARCH="$(uname -m)"
case "$HOST_ARCH" in
    arm64)  HOST_RID="osx-arm64" ;;
    x86_64) HOST_RID="osx-x64"   ;;
    *)      HOST_RID=""          ;;
esac
EFFECTIVE_RID="${RID:-$HOST_RID}"

# Short label for dist paths and DMG file names (arm64 | x64).
arch_label_from_rid() {
    case "$1" in
        osx-arm64) echo "arm64" ;;
        osx-x64)   echo "x64" ;;
        *)
            case "$(uname -m)" in
                arm64|aarch64) echo "arm64" ;;
                x86_64) echo "x64" ;;
                *) echo "unknown" ;;
            esac
            ;;
    esac
}

ARCH_LABEL="$(arch_label_from_rid "$EFFECTIVE_RID")"
[ "$ARCH_LABEL" != "unknown" ] || die "Set RID=osx-arm64 or RID=osx-x64 (host arch could not be inferred)."

die() {
    echo "error: $*" >&2
    exit 1
}

# The .NET macOS SDK may leave a RID-specific stub under osx-arm64/ and the full
# processed .app one level up (net10.0-macos/SparkleShare.app). Prefer the complete bundle.
app_bundle_is_complete() {
    local app="$1"

    [ -d "$app" ] || return 1

    for required in \
        "Contents/Resources/sparkleshare-app.icns" \
        "Contents/Resources/MainMenu.nib" \
        "Contents/MacOS/SparkleShare" \
        "Contents/MonoBundle/libcoreclr.dylib"; do
        [ -e "$app/$required" ] || return 1
    done

    local resource_count
    resource_count="$(find "$app/Contents/Resources" -maxdepth 1 -type f 2>/dev/null | wc -l | tr -d ' ')"
    [ "$resource_count" -ge 10 ]
}

resolve_app_bundle_path() {
    local config="$1"
    local base="$MAC_DIR/bin/$config/net10.0-macos"
    local tried=()
    local app

    # When RID is set, only use the RID-specific bundle. The shared
    # net10.0-macos/SparkleShare.app is often left from the previous arch build
    # (wrong CPU type and stale Info.plist).
    if [ -n "$RID" ]; then
        tried+=("$base/$EFFECTIVE_RID/SparkleShare.app")
    else
        if [ -n "$EFFECTIVE_RID" ]; then
            tried+=("$base/$EFFECTIVE_RID/SparkleShare.app")
        fi
        tried+=("$base/SparkleShare.app")
    fi

    for app in "${tried[@]}"; do
        if app_bundle_is_complete "$app"; then
            printf '%s' "$app"
            return 0
        fi
    done

    echo "Checked app bundle paths:" >&2
    for app in "${tried[@]}"; do
        echo "  $app" >&2
    done
    return 1
}


verify_app_bundle_architecture() {
    local app="$1"
    local bin="$app/Contents/MacOS/SparkleShare"

    [ -f "$bin" ] || die "Missing executable: $bin"
    command -v lipo >/dev/null 2>&1 || return 0

    local archs
    archs="$(lipo -archs "$bin" 2>/dev/null | tr ' ' '\n' | sort -u | tr '\n' ' ')"

    case "$ARCH_LABEL" in
        arm64)
            echo "$archs" | grep -q 'arm64' || die "Expected arm64 binary, got: $archs (wrong .app bundle — rebuild with RID=osx-arm64)"
            ;;
        x64)
            echo "$archs" | grep -q 'x86_64' || die "Expected x86_64 binary, got: $archs (wrong .app bundle — rebuild with RID=osx-x64)"
            ;;
    esac

    echo "Binary architecture: $archs"
}

verify_app_bundle() {
    local app="$1"
    local missing=0

    for required in \
        "Contents/Resources/sparkleshare-app.icns" \
        "Contents/Resources/MainMenu.nib" \
        "Contents/MacOS/SparkleShare" \
        "Contents/MonoBundle/libcoreclr.dylib"; do
        if [ ! -e "$app/$required" ]; then
            echo "  missing: $required" >&2
            missing=1
        fi
    done

    if [ "$missing" -ne 0 ]; then
        die "App bundle is incomplete. Run a clean Release build first:\n  dotnet clean SparkleShare/Mac/SparkleShare.Mac.csproj -c Release\n  CONFIG=Release scripts/build-mac.sh"
    fi

    local resource_count
    resource_count="$(find "$app/Contents/Resources" -maxdepth 1 -type f | wc -l | tr -d ' ')"
    if [ "$resource_count" -lt 10 ]; then
        die "App bundle has too few top-level resources ($resource_count). Expected icons, nib, PNGs, etc.\nRun a clean Release build before signing."
    fi
}

require_config() {
    if [[ "$SIGNING_IDENTITY" == *'Your Name'* ]] || [[ -z "$SIGNING_IDENTITY" ]]; then
        die "Set SIGNING_IDENTITY in $LOCAL_CONFIG (copy from sign-pack-notarize-mac.local.sh.example) or export it in the environment."
    fi
    if [[ "$NOTARY_KEYCHAIN_PROFILE" == 'YOUR_NOTARY_PROFILE' ]] || [[ -z "$NOTARY_KEYCHAIN_PROFILE" ]]; then
        die "Set NOTARY_KEYCHAIN_PROFILE in $LOCAL_CONFIG or export it in the environment."
    fi
    [ -f "$ENTITLEMENTS" ] || die "Missing entitlements file: $ENTITLEMENTS"
}

read_version() {
    local version=""
    if [ -f "$CSPROJ" ]; then
        version="$(grep -m1 '<ApplicationDisplayVersion>' "$CSPROJ" \
            | sed -E 's/.*<ApplicationDisplayVersion>([^<]+)<.*/\1/' \
            | tr -d '[:space:]')"
    fi
    if [ -z "$version" ] && [ -n "${APP_PATH:-}" ] && [ -d "$APP_PATH" ]; then
        version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' \
            "$APP_PATH/Contents/Info.plist" 2>/dev/null || true)"
    fi
    [ -n "$version" ] || die "Could not determine version (ApplicationDisplayVersion or Info.plist)."
    version="${version//\//-}"
    version="${version// /_}"
    printf '%s' "$version"
}

is_mach_o() {
    file "$1" 2>/dev/null | grep -q 'Mach-O'
}

sign_file() {
    local target="$1"
    local entitlements="${2:-}"

    if [ -n "$entitlements" ]; then
        codesign --force --options runtime --timestamp \
            --entitlements "$entitlements" \
            --sign "$SIGNING_IDENTITY" \
            "$target"
    else
        codesign --force --options runtime --timestamp \
            --sign "$SIGNING_IDENTITY" \
            "$target"
    fi
}

sign_mach_o_tree() {
    local dir="$1"
    local entitlements="${2:-}"

    [ -d "$dir" ] || return 0

    while IFS= read -r -d '' path; do
        if is_mach_o "$path"; then
            echo "  signing $(basename "$path")"
            sign_file "$path" "$entitlements"
        fi
    done < <(find "$dir" -type f -print0)
}

sign_app_bundle() {
    local app="$1"

    echo "Signing .NET runtime (Contents/MonoBundle)…"
    sign_mach_o_tree "$app/Contents/MonoBundle"

    echo "Signing bundled Git (Contents/Resources/git)…"
    sign_mach_o_tree "$app/Contents/Resources/git"

    echo "Signing frameworks and plug-ins…"
    sign_mach_o_tree "$app/Contents/Frameworks"
    sign_mach_o_tree "$app/Contents/PlugIns"

    echo "Signing main executable (Contents/MacOS)…"
    sign_mach_o_tree "$app/Contents/MacOS" "$ENTITLEMENTS"

    echo "Signing SparkleShare.app…"
    sign_file "$app" "$ENTITLEMENTS"

    echo "Verifying signature…"
    codesign --verify --deep --strict --verbose=2 "$app"
}

# Sign/staple in dist/ so bin/Release stays writable for the next dotnet build.
prepare_dist_app() {
    local build_app="$1"
    local dist_app="$2"

    rm -rf "$dist_app"
    ditto "$build_app" "$dist_app"
}

# hdiutil fails with "Resource busy" when /Volumes/SparkleShare is still mounted
# from a previous open/test — not because the output .dmg file exists.
detach_sparkleshare_volumes() {
    local vol

    for vol in /Volumes/SparkleShare*; do
        [ -e "$vol" ] || continue
        echo "Unmounting $vol…"
        hdiutil detach "$vol" -force -quiet 2>/dev/null \
            || diskutil unmount force "$vol" >/dev/null 2>&1 \
            || true
    done
}


create_dmg() {
    local app="$1"
    local dmg_path="$2"
    local staging
    local volname="SparkleShare-${ARCH_LABEL}"

    detach_sparkleshare_volumes

    staging="$(mktemp -d)"

    echo "Staging DMG (app + Applications alias)…"
    ditto "$app" "$staging/SparkleShare.app"
    ln -s /Applications "$staging/Applications"

    rm -f "$dmg_path"
    hdiutil create \
        -volname "$volname" \
        -srcfolder "$staging" \
        -ov \
        -format UDZO \
        "$dmg_path"
    rm -rf "$staging"

    echo "Signing DMG…"
    codesign --force --timestamp \
        --sign "$SIGNING_IDENTITY" \
        "$dmg_path"

    codesign --verify --verbose=2 "$dmg_path"
}

verify_dmg_mounts() {
    local dmg_path="$1"
    local mount_dir
    mount_dir="$(mktemp -d)"

    hdiutil attach "$dmg_path" -mountpoint "$mount_dir" -nobrowse -quiet
    codesign --verify --deep --strict "$mount_dir/SparkleShare.app"
    hdiutil detach "$mount_dir" -quiet
    rmdir "$mount_dir"
}

main() {
    require_config

    APP_PATH="$(resolve_app_bundle_path "$CONFIG")" || die "No complete SparkleShare.app found for RID=${EFFECTIVE_RID:-<default>} under $MAC_DIR/bin/$CONFIG/net10.0-macos/\nRun: CONFIG=Release RID=${EFFECTIVE_RID:-osx-arm64} scripts/build-mac.sh"

    VERSION="$(read_version)"
    DMG_NAME="SparkleShare_${VERSION}_${ARCH_LABEL}.dmg"
    DMG_PATH="$DIST_DIR/$DMG_NAME"
    DIST_APP="$DIST_DIR/$ARCH_LABEL/SparkleShare.app"

    mkdir -p "$DIST_DIR/$ARCH_LABEL"

    echo "Build output:  $APP_PATH"
    echo "Architecture:  $ARCH_LABEL (RID: ${EFFECTIVE_RID:-<default>})"
    echo "Dist app:      $DIST_APP"
    echo "Version:       $VERSION"
    echo "Release DMG:   $DMG_PATH"
    echo

    verify_app_bundle "$APP_PATH"
    verify_app_bundle_architecture "$APP_PATH"

    local plist_version=""
    plist_version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' \
        "$APP_PATH/Contents/Info.plist" 2>/dev/null || true)"
    if [ -n "$plist_version" ]; then
        echo "App bundle version: $plist_version"
    fi

    echo "Copying app bundle to dist/ (keeps bin/Release rebuildable)…"
    prepare_dist_app "$APP_PATH" "$DIST_APP"

    sign_app_bundle "$DIST_APP"

    echo
    create_dmg "$DIST_APP" "$DMG_PATH"

    echo
    echo "Submitting to Apple notary service (profile: $NOTARY_KEYCHAIN_PROFILE)…"
    SUBMIT_OUTPUT="$(xcrun notarytool submit "$DMG_PATH" \
        --keychain-profile "$NOTARY_KEYCHAIN_PROFILE" \
        --wait 2>&1)" || true
    echo "$SUBMIT_OUTPUT"

    if echo "$SUBMIT_OUTPUT" | grep -q 'status: Invalid'; then
        SUBMISSION_ID="$(echo "$SUBMIT_OUTPUT" | sed -n 's/^  id: //p' | head -1)"
        if [ -n "$SUBMISSION_ID" ]; then
            echo
            echo "Notarization log:"
            xcrun notarytool log "$SUBMISSION_ID" \
                --keychain-profile "$NOTARY_KEYCHAIN_PROFILE" || true
        fi
        die "Notarization failed (status: Invalid)."
    fi

    echo
    echo "Stapling notarization ticket to DMG…"
    xcrun stapler staple "$DMG_PATH"
    xcrun stapler validate "$DMG_PATH"

    echo "Verifying mounted app signature…"
    verify_dmg_mounts "$DMG_PATH"

    echo
    echo "Done."
    echo "  Build output (unsigned, for rebuilds): $APP_PATH"
    echo "  Signed dist app:                       $DIST_APP"
    echo "  Release DMG (distribute this):         $DMG_PATH"
    echo
    echo "Users open the DMG and drag SparkleShare to Applications."
    echo
    echo "Optional Gatekeeper check after mount:"
    echo "  open \"$DMG_PATH\""
    echo "  spctl -a -t exec -vv \"/Volumes/SparkleShare-${ARCH_LABEL}/SparkleShare.app\""
    echo
    echo "If hdiutil reports 'Resource busy', unmount stale volumes:"
    echo "  hdiutil detach /Volumes/SparkleShare-${ARCH_LABEL} -force"
    echo "  # or: ls /Volumes/SparkleShare*"
}

main "$@"
