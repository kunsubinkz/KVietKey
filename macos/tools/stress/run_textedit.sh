#!/usr/bin/env bash
# Gõ văn bản dài vào TextEdit thật (tài liệu mới) bằng phím thật qua event tap, đọc lại bằng Accessibility và so với bản gốc.
# Dùng: tools/stress/run_textedit.sh [--method telex|vni|viqr|combined] [--delay-ms N] [--rollover-ms N] [--jitter-ms N] [--trace]
# Lưu ý: trong lúc chạy TextEdit phải ở phía trước, đừng gõ hay bấm gì khác.
set -uo pipefail
cd "$(dirname "$0")"
METHOD=telex; ARGS=()
while [[ $# -gt 0 ]]; do case "$1" in --method) METHOD="$2"; ARGS+=("$1" "$2"); shift 2;; *) ARGS+=("$1"); shift;; esac; done
W="${TMPDIR:-/tmp}/kvietkey-textedit"; mkdir -p "$W"; rm -f "$W"/out.txt*
python3 -I make_keys.py text.txt "$W/keys.txt" "$W/expected.txt" "$METHOD" >/dev/null
pkill -x KVietKey 2>/dev/null; sleep 1
open -n /Applications/KVietKey.app --args --textedit-test "$W/out.txt" --keys "$W/keys.txt" --expected "$W/expected.txt" --old-tone ${ARGS[@]+"${ARGS[@]}"}
for _ in $(seq 1 900); do [ -f "$W/out.txt" ] && break; sleep 1; done
sleep 1
cat "$W/out.txt" 2>/dev/null | head -3
python3 -I - "$W" <<'PY'
import difflib, sys, os, unicodedata
w = sys.argv[1]
if not os.path.exists(f'{w}/out.txt.got'): print('Không có kết quả'); sys.exit(0)
g = unicodedata.normalize('NFC', open(f'{w}/out.txt.got', encoding='utf8').read())
e = open(f'{w}/expected.txt', encoding='utf8').read()
g = g.replace('\r', '\n')
sm = difflib.SequenceMatcher(None, e, g, autojunk=False)
diffs = [(i1, i2, j1, j2) for t, i1, i2, j1, j2 in sm.get_opcodes() if t != 'equal']
print(('GIỐNG' if not diffs else 'KHÁC'), f'{len(diffs)} chỗ ({len(g)}/{len(e)} ký tự)')
for i1, i2, j1, j2 in diffs[:25]:
    print(('  …%s[%s → %s]%s…' % (e[max(0,i1-10):i1], e[i1:i2], g[j1:j2], e[i2:i2+8])).replace('\n', '⏎'))
PY
pkill -x KVietKey 2>/dev/null; sleep 1; open /Applications/KVietKey.app
