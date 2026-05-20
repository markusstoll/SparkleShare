#!/usr/bin/env bash
#
# Sign, pack, and notarize the SparkleShare macOS app bundle for distribution.
#
# One-time notary credentials (app-specific password from appleid.apple.com):
#   xcrun notarytool store-credentials "YOUR_PROFILE_NAME" \
#     --apple-id "you@example.com" \
#     --team-id "YOUR_TEAM_ID" \
#     --password "xxxx-xxxx-xxxx-xxxx"
#
# Build first:
#   CONFIG=Release scripts/build-mac.sh
#
# Then:
#   scripts/sign-pack-notarize-mac.sh
#
set -euo pipefail

# --- Edit these two values ---------------------------------------------------
SIGNING_IDENTITY='Developer ID Application: Your Name (TEAMID)'
NOTARY_KEYCHAIN_PROFILE='YOUR_NOTARY_PROFILE'
# -----------------------------------------------------------------------------

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
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
APP_PATH="$MAC_DIR/bin/$CONFIG/net10.0-macos/$EFFECTIVE_RID/SparkleShare.app"

die() {
    echo "error: $*" >&2
    exit 1
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
        die "Set SIGNING_IDENTITY at the top of this script (Developer ID Application certificate)."
    fi
    if [[ "$NOTARY_KEYCHAIN_PROFILE" == 'YOUR_NOTARY_PROFILE' ]] || [[ -z "$NOTARY_KEYCHAIN_PROFILE" ]]; then
        die "Set NOTARY_KEYCHAIN_PROFILE at the top of this script (notarytool keychain profile name)."
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
    if [ -z "$version" ] && [ -d "$APP_PATH" ]; then
        version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' \
            "$APP_PATH/Contents/Info.plist" 2>/dev/null || true)"
    fi
    [ -n "$version" ] || die "Could not determine version (ApplicationDisplayVersion or Info.plist)."
    # Safe for filenames (keep dots and hyphens).
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

sign_nested_app() {
    local nested_app="$1"
    [ -d "$nested_app" ] || return 0

    echo "Signing nested app: $(basename "$nested_app")"
    sign_mach_o_tree "$nested_app/Contents/MonoBundle"
    sign_mach_o_tree "$nested_app/Contents/MacOS"
    sign_mach_o_tree "$nested_app/Contents/Frameworks"
    sign_mach_o_tree "$nested_app/Contents/PlugIns"
    sign_file "$nested_app"
}

sign_app_bundle() {
    local app="$1"

    sign_nested_app "$app/Contents/Resources/SparkleShareInviteOpener.app"

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

create_zip() {
    local app="$1"
    local zip_path="$2"

    rm -f "$zip_path"
    ditto -c -k --keepParent "$app" "$zip_path"
}

main() {
    require_config

    [ -d "$APP_PATH" ] || die "App bundle not found: $APP_PATH\nRun: CONFIG=Release scripts/build-mac.sh"

    VERSION="$(read_version)"
    ZIP_NAME="SparkleShare_${VERSION}.zip"
    ZIP_PATH="$DIST_DIR/$ZIP_NAME"

    mkdir -p "$DIST_DIR"

    echo "App:     $APP_PATH"
    echo "Version: $VERSION"
    echo "Output:  $ZIP_PATH"
    echo

    verify_app_bundle "$APP_PATH"

    sign_app_bundle "$APP_PATH"

    echo
    echo "Creating zip for notarization…"
    create_zip "$APP_PATH" "$ZIP_PATH"

    echo
    echo "Submitting to Apple notary service (profile: $NOTARY_KEYCHAIN_PROFILE)…"
    SUBMIT_OUTPUT="$(xcrun notarytool submit "$ZIP_PATH" \
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
    echo "Stapling notarization ticket to app bundle…"
    xcrun stapler staple "$APP_PATH"
    xcrun stapler validate "$APP_PATH"

    echo
    echo "Recreating distribution zip with stapled app…"
    create_zip "$APP_PATH" "$ZIP_PATH"

    echo
    echo "Done."
    echo "  Signed app:  $APP_PATH"
    echo "  Release zip: $ZIP_PATH"
    echo
    echo "Extract the zip with ditto (preserves bundle metadata):"
    echo "  ditto -x -k \"$ZIP_PATH\" /path/to/destination/"
    echo
    echo "Optional local Gatekeeper check:"
    echo "  spctl -a -t exec -vv \"$APP_PATH\""
}

main "$@"
