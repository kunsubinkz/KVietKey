#!/usr/bin/env bash
# Gõ văn bản dài bằng phím thật qua event tap rồi so với bản gốc. Cần quyền Accessibility cho /Applications/KVietKey.app.
# Dùng: tools/stress/run.sh [--old-tone] [--delay-ms N]
set -euo pipefail
cd "$(dirname "$0")"
W="${TMPDIR:-/tmp}/kvietkey-stress"; mkdir -p "$W"; rm -f "$W/out.txt" "$W/out.txt.got"
python3 -I make_keys.py text.txt "$W/keys.txt" "$W/expected.txt"
pkill -x KVietKey 2>/dev/null || true; sleep 1
open -n /Applications/KVietKey.app --args --live-test "$W/out.txt" --keys "$W/keys.txt" "$@"
for _ in $(seq 1 240); do [ -f "$W/out.txt" ] && break; sleep 1; done
sleep 1
python3 -I - "$W" <<'PY'
import difflib, sys, unicodedata
w = sys.argv[1]
g = unicodedata.normalize('NFC', open(f'{w}/out.txt.got', encoding='utf8').read())
e = open(f'{w}/expected.txt', encoding='utf8').read()
sm = difflib.SequenceMatcher(None, e, g, autojunk=False)
n = 0
for tag, i1, i2, j1, j2 in sm.get_opcodes():
    if tag != 'equal':
        n += 1
        print(f"…{e[max(0,i1-12):i1]}[{e[i1:i2]} → {g[j1:j2]}]{e[i2:i2+10]}…".replace('\n', '⏎'))
print('GIỐNG HOÀN TOÀN' if g == e else f'KHÁC {n} chỗ', f'({len(g)}/{len(e)} ký tự)')
PY
open /Applications/KVietKey.app
