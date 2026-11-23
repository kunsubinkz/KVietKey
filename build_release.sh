#!/usr/bin/env bash
# Build KVietKey.app (Release), ký, đóng gói ZIP + DMG và (tùy chọn) notarize.
#
# Dùng:
#   ./build_release.sh                       # arm64, ký ad-hoc, chạy test engine trước
#   ./build_release.sh --universal           # arm64 + x86_64 (Universal 2)
#   ./build_release.sh --skip-tests          # bỏ qua unit test
#   ./build_release.sh --version 1.2.0       # đặt phiên bản (mặc định: đọc file VERSION hoặc 1.0.0)
#
# Biến môi trường:
#   SIGN_IDENTITY   Tên chứng chỉ, vd. "Developer ID Application: Ten (TEAMID)". Mặc định "-" (ad-hoc).
#   NOTARY_PROFILE  Tên keychain profile của `xcrun notarytool store-credentials`. Đặt để bật notarize.
#   BUILD_NUMBER    Số build (mặc định: yyyymmddHHMM).
set -euo pipefail

cd "$(dirname "$0")"
ROOT="$(pwd)"

APP_NAME="KVietKey"
ARCHS=(arm64)
RUN_TESTS=1
VERSION="${VERSION:-}"
SIGN_IDENTITY="${SIGN_IDENTITY:--}"
NOTARY_PROFILE="${NOTARY_PROFILE:-}"
BUILD_NUMBER="${BUILD_NUMBER:-$(date +%Y%m%d%H%M)}"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --universal) ARCHS=(arm64 x86_64) ;;
        --skip-tests) RUN_TESTS=0 ;;
        --version) VERSION="$2"; shift ;;
        -h|--help) sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "Tham số không hợp lệ: $1" >&2; exit 2 ;;
    esac
    shift
done

if [[ -z "$VERSION" ]]; then
    if [[ -f VERSION ]]; then VERSION="$(tr -d '[:space:]' < VERSION)"; else VERSION="1.0.0"; fi
fi

ARCH_LABEL="$(IFS=-; echo "${ARCHS[*]}")"
[[ ${#ARCHS[@]} -gt 1 ]] && ARCH_LABEL="universal"
OUT="$ROOT/build/release"
APP="$OUT/$APP_NAME.app"
ZIP="$OUT/$APP_NAME-$VERSION-$ARCH_LABEL.zip"
DMG="$OUT/$APP_NAME-$VERSION-$ARCH_LABEL.dmg"

step() { printf '\n\033[1;34m==> %s\033[0m\n' "$*"; }
die() { printf '\033[1;31mLỗi: %s\033[0m\n' "$*" >&2; exit 1; }

command -v swift >/dev/null || die "Không tìm thấy swift (cài Xcode hoặc Command Line Tools)."
[[ "$(uname -s)" == "Darwin" ]] || die "Chỉ build được trên macOS."

if [[ $RUN_TESTS -eq 1 ]]; then
    step "Chạy unit test VietEngine"
    if ! TEST_LOG="$(cd Packages/VietEngine && swift test 2>&1)"; then
        echo "$TEST_LOG" | tail -n 40
        die "Unit test thất bại."
    fi
    echo "$TEST_LOG" | grep -E "Executed [0-9]+ tests" | tail -n 1
fi

step "Build Release (${ARCHS[*]})"
ARCH_FLAGS=()
for a in "${ARCHS[@]}"; do ARCH_FLAGS+=(--arch "$a"); done
swift build -c release "${ARCH_FLAGS[@]}"
BIN_DIR="$(swift build -c release "${ARCH_FLAGS[@]}" --show-bin-path)"
BIN="$BIN_DIR/$APP_NAME"
[[ -x "$BIN" ]] || die "Không thấy file thực thi: $BIN"

step "Dựng $APP_NAME.app"
rm -rf "$OUT"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/$APP_NAME"
sed -e "s/__VERSION__/$VERSION/" -e "s/__BUILD__/$BUILD_NUMBER/" Resources/Info.plist > "$APP/Contents/Info.plist"
printf 'APPL????' > "$APP/Contents/PkgInfo"
if [[ ! -f Resources/AppIcon.icns ]]; then
    swift tools/make_icon.swift Resources/AppIcon.icns
fi
cp Resources/AppIcon.icns "$APP/Contents/Resources/AppIcon.icns"
plutil -lint "$APP/Contents/Info.plist" >/dev/null

step "Ký mã (identity: $SIGN_IDENTITY)"
SIGN_ARGS=(--force --options runtime --sign "$SIGN_IDENTITY" --identifier vn.kvietkey.app)
[[ "$SIGN_IDENTITY" != "-" ]] && SIGN_ARGS+=(--timestamp)
codesign "${SIGN_ARGS[@]}" "$APP"
codesign --verify --strict --verbose=2 "$APP"
echo "Kiến trúc: $(lipo -archs "$APP/Contents/MacOS/$APP_NAME")"

step "Smoke test (--selftest)"
"$APP/Contents/MacOS/$APP_NAME" --selftest || die "Smoke test thất bại."

step "Đóng gói ZIP"
ditto -c -k --keepParent "$APP" "$ZIP"

step "Đóng gói DMG"
STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
hdiutil create -volname "$APP_NAME" -srcfolder "$STAGE" -ov -format UDZO "$DMG" >/dev/null
if [[ "$SIGN_IDENTITY" != "-" ]]; then codesign --force --sign "$SIGN_IDENTITY" --timestamp "$DMG"; fi

if [[ -n "$NOTARY_PROFILE" ]]; then
    [[ "$SIGN_IDENTITY" != "-" ]] || die "Notarize cần SIGN_IDENTITY là chứng chỉ Developer ID."
    step "Notarize (profile: $NOTARY_PROFILE)"
    xcrun notarytool submit "$DMG" --keychain-profile "$NOTARY_PROFILE" --wait
    xcrun stapler staple "$DMG"
    xcrun stapler staple "$APP"
    rm -f "$ZIP"
    ditto -c -k --keepParent "$APP" "$ZIP"
fi

step "Checksum"
(cd "$OUT" && shasum -a 256 "$(basename "$ZIP")" "$(basename "$DMG")" | tee SHA256SUMS.txt)

step "Hoàn tất"
echo "App: $APP"
echo "ZIP: $ZIP"
echo "DMG: $DMG"
if [[ "$SIGN_IDENTITY" == "-" ]]; then
    cat <<EOF

Lưu ý: bản ký ad-hoc chỉ dành cho máy của bạn. Khi mở lần đầu: chuột phải → Open,
sau đó cấp quyền Accessibility cho KVietKey (cần cấp lại sau mỗi lần build mới).
Để phát hành rộng rãi hãy đặt SIGN_IDENTITY (Developer ID) và NOTARY_PROFILE.
EOF
fi
