#!/usr/bin/env bash
# Test đầy đủ bằng phím thật qua event tap: mọi kiểu gõ, mọi bảng mã, và bộ test tính năng.
# Cần: app đã cài ở /Applications/KVietKey.app và được cấp quyền Accessibility.
# Dùng: tools/stress/run_all.sh [delay_ms]   (mặc định 4 ms mỗi phím)
set -uo pipefail
cd "$(dirname "$0")"
DELAY="${1:-4}"
W="${TMPDIR:-/tmp}/kvietkey-full"; mkdir -p "$W"
APP=/Applications/KVietKey.app

run_live() { # $1=nhãn; các tham số còn lại truyền cho --live-test
    local label="$1"; shift
    rm -f "$W/out.txt" "$W/out.txt.got" "$W/out.txt.exp"
    pkill -x KVietKey 2>/dev/null; sleep 1
    open -n "$APP" --args --live-test "$W/out.txt" "$@"
    for _ in $(seq 1 400); do [ -f "$W/out.txt" ] && break; sleep 1; done
    sleep 1
    grep -E '^(ABORT|focus lấy lại)' "$W/out.txt" 2>/dev/null | head -3 | sed 's/^/        ⚠ /'
}

compare() { # $1=nhãn
    python3 -I - "$W" "$1" <<'PY'
import difflib, sys, os, unicodedata
w, label = sys.argv[1], sys.argv[2]
got_p, exp_p = f'{w}/out.txt.got', f'{w}/out.txt.exp'
if not os.path.exists(got_p):
    print(f'LỖI   {label}: không có kết quả (thiếu quyền Accessibility hoặc app không chạy)'); sys.exit(0)
g = open(got_p, encoding='utf8').read()
if os.path.exists(exp_p):
    e = open(exp_p, encoding='utf8').read()
else:
    e = open(f'{w}/expected.txt', encoding='utf8').read()
    g = unicodedata.normalize('NFC', g)
sm = difflib.SequenceMatcher(None, e, g, autojunk=False)
diffs = [(i1, i2, j1, j2) for t, i1, i2, j1, j2 in sm.get_opcodes() if t != 'equal']
print(('PASS  ' if not diffs else f'DIFF  ') + f'{label}: {len(diffs)} chỗ khác ({len(g)}/{len(e)} ký tự)')
for i1, i2, j1, j2 in diffs[:8]:
    print(('        …%s[%s → %s]%s…' % (e[max(0,i1-10):i1], e[i1:i2], g[j1:j2], e[i2:i2+8])).replace('\n', '⏎'))
PY
}

stress() { # $1=nhãn $2=method(encoder) $3=method(app) $4=charset
    local label="$1" enc="$2" appm="$3" cs="${4:-unicode}"
    python3 -I make_keys.py text.txt "$W/keys.txt" "$W/expected.txt" "$enc" >/dev/null
    run_live "$label" --keys "$W/keys.txt" --expected "$W/expected.txt" --method "$appm" --charset "$cs" --old-tone --delay-ms "$DELAY"
    compare "$label"
}

echo "== Kiểu gõ (văn bản 4.106 ký tự, $DELAY ms/phím) =="
stress "Telex"                           telex telex
stress "VNI"                             vni   vni
stress "Kết hợp (gõ bằng phím Telex)"    telex combined
stress "Kết hợp (gõ bằng phím VNI)"      vni   combined
stress "VIQR (chỉ chữ, bỏ dấu câu)"      viqr  viqr
echo "== Bảng mã (gõ Telex) =="
for cs in tcvn3 vniWindows viscii cp1258 unicodeComposite; do stress "Bảng mã $cs" telex telex "$cs"; done
echo "== Tính năng =="
run_live suite --suite
grep -E '^(PASS|FAIL|SUITE|NO-|NOT-)' "$W/out.txt" || echo "LỖI   suite: không có kết quả"
pkill -x KVietKey 2>/dev/null; sleep 1; open "$APP"
