#!/usr/bin/env bash
# Build KVietKey cho Windows ngay trên macOS/Linux: test → publish (x64, arm64) → ký → bộ cài NSIS + ZIP portable.
#
# Dùng:
#   ./build_release.sh                       # test + cả x64 và arm64
#   ./build_release.sh --arch x64            # chỉ một kiến trúc (x64 | arm64 | all)
#   ./build_release.sh --skip-tests          # bỏ qua dotnet test
#   ./build_release.sh --version 1.2.0       # đặt phiên bản (mặc định: đọc ../VERSION)
#
# Biến môi trường (tùy chọn, để ký Authenticode bằng osslsigncode):
#   SIGN_PFX        đường dẫn file .pfx
#   SIGN_PASSWORD   mật khẩu của .pfx
#   SIGN_TIMESTAMP  URL timestamp (mặc định http://timestamp.digicert.com)
#
# Yêu cầu: .NET 10 SDK, makensis (brew install makensis), zip. Tùy chọn: osslsigncode (brew install osslsigncode).
set -euo pipefail

cd "$(dirname "$0")"
ROOT="$(pwd)"

ARCHS=(x64 arm64)
RUN_TESTS=1
VERSION="${VERSION:-}"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --arch)
            case "$2" in
                all) ARCHS=(x64 arm64) ;;
                x64|arm64) ARCHS=("$2") ;;
                *) echo "--arch phải là x64, arm64 hoặc all" >&2; exit 2 ;;
            esac
            shift ;;
        --skip-tests) RUN_TESTS=0 ;;
        --version) VERSION="$2"; shift ;;
        -h|--help) sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "Tham số không hợp lệ: $1" >&2; exit 2 ;;
    esac
    shift
done

if [[ -z "$VERSION" ]]; then
    if [[ -f ../VERSION ]]; then VERSION="$(tr -d '[:space:]' < ../VERSION)"; else VERSION="1.0.0"; fi
fi
[[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "Phiên bản phải dạng x.y.z, nhận được: $VERSION" >&2; exit 2; }

command -v dotnet >/dev/null || { echo "Thiếu dotnet (.NET 10 SDK)" >&2; exit 1; }
command -v makensis >/dev/null || { echo "Thiếu makensis: brew install makensis" >&2; exit 1; }
command -v zip >/dev/null || { echo "Thiếu zip" >&2; exit 1; }

OUT="$ROOT/artifacts/release"
STAGE="$ROOT/artifacts/stage"
ICON="$ROOT/src/KVietKey.App/Assets/kvietkey.ico"
LICENSE_FILE="$ROOT/../LICENSE"
FONT_LICENSE="$ROOT/src/KVietKey.App/Assets/Fonts/OFL.txt"
APP_PROJECT="$ROOT/src/KVietKey.App"

rm -rf "$OUT" "$STAGE"
mkdir -p "$OUT" "$STAGE"

if [[ "$RUN_TESTS" == 1 ]]; then
    echo "==> Test (engine + core)"
    dotnet test KVietKey.slnx -c Release --nologo -v q
fi

can_sign=0
if [[ -n "${SIGN_PFX:-}" ]]; then
    command -v osslsigncode >/dev/null || { echo "SIGN_PFX được đặt nhưng thiếu osslsigncode: brew install osslsigncode" >&2; exit 1; }
    can_sign=1
fi

sign() {
    local file="$1"
    [[ "$can_sign" == 1 ]] || return 0
    echo "    ký $(basename "$file")"
    osslsigncode sign -pkcs12 "$SIGN_PFX" -pass "${SIGN_PASSWORD:-}" -n "KVietKey" -i "https://github.com/kunsubinkz/KVietKey" \
        -t "${SIGN_TIMESTAMP:-http://timestamp.digicert.com}" -h sha256 -in "$file" -out "$file.signed" >/dev/null
    mv "$file.signed" "$file"
}

for arch in "${ARCHS[@]}"; do
    rid="win-$arch"
    pub="$STAGE/publish-$arch"
    pkg="$STAGE/package-$arch"
    echo "==> Publish $rid"
    dotnet publish "$APP_PROJECT" -c Release -r "$rid" --self-contained \
        -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:EnableCompressionInSingleFile=true \
        -p:DebugType=None -p:DebugSymbols=false -p:Version="$VERSION" --nologo -v q -o "$pub"

    mkdir -p "$pkg/licenses"
    cp "$pub"/KVietKey.exe "$pub"/*.dll "$pkg/"
    cp "$FONT_LICENSE" "$pkg/licenses/OFL-BeVietnamPro.txt"
    sign "$pkg/KVietKey.exe"

    installer="$OUT/KVietKey-$VERSION-win-$arch-setup.exe"
    echo "==> Bộ cài NSIS ($arch)"
    makensis -V2 -INPUTCHARSET UTF8 \
        -DVERSION="$VERSION" -DARCH="$arch" -DSRC="$pkg" -DOUTFILE="$installer" \
        -DICON="$ICON" -DLICENSE="$LICENSE_FILE" installer/kvietkey.nsi
    sign "$installer"

    echo "==> ZIP portable ($arch)"
    portable="$STAGE/portable-$arch/KVietKey"
    mkdir -p "$portable"
    cp -R "$pkg"/. "$portable/"
    cp "$LICENSE_FILE" "$portable/LICENSE.txt"
    # Có portable.txt cạnh exe thì cài đặt được lưu trong thư mục data\ cạnh exe thay vì %APPDATA%.
    echo "Xóa file này nếu muốn lưu cài đặt trong %APPDATA%\\KVietKey." > "$portable/portable.txt"
    (cd "$STAGE/portable-$arch" && zip -qr -9 "$OUT/KVietKey-$VERSION-win-$arch-portable.zip" KVietKey)
done

echo "==> SHA256"
(cd "$OUT" && shasum -a 256 KVietKey-* > SHA256SUMS && cat SHA256SUMS)

rm -rf "$STAGE"
echo
echo "Xong. Kết quả trong $OUT"
if [[ "$can_sign" == 0 ]]; then
    echo "Lưu ý: chưa ký số (đặt SIGN_PFX/SIGN_PASSWORD) nên Windows SmartScreen sẽ cảnh báo khi cài."
fi
