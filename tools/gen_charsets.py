#!/usr/bin/env python3
"""Sinh Packages/VietEngine/Sources/VietEngine/Charset/CharsetTables.swift.

Nguồn: bảng mã công khai (TCVN3, VNI Windows, CP1258) lấy từ cột thứ tự chuẩn của
OpenKey (Vietnamese.cpp) và VISCII từ `iconv`. Chỉ trích xuất dữ liệu ánh xạ ký tự.
Dùng: python3 tools/gen_charsets.py /path/Vietnamese.cpp > CharsetTables.swift
"""
import re, subprocess, sys

src = open(sys.argv[1], encoding="utf-8").read()
start = src.index("_codeTable[] = {")
body = src[start:src.index("// sắc, huyền", start)]
blocks = re.split(r"\n    \{\s*//", body)[1:]
assert len(blocks) == 5, len(blocks)

def rows(block):
    out = []
    for m in re.finditer(r"\{\s*KEY_\w+(?:\|\w+)?,\s*\{([^}]*)\}\}", block):
        out.append([int(x.strip(), 16) for x in m.group(1).split(",") if x.strip()])
    return out

tables = [rows(b) for b in blocks]
uni = tables[0]

def build(idx, two_byte):
    res = {}
    for urow, lrow in zip(uni, tables[idx]):
        assert len(urow) == len(lrow)
        for u, l in zip(urow, lrow):
            if l == 0:
                continue
            bs = [l & 0xFF] if l <= 0xFF else [l & 0xFF, l >> 8]
            res[u] = bs
    return res

# Sáu chữ hoa của VISCII nằm ở vị trí mã điều khiển nên iconv không xuất được.
VISCII_CONTROL = {0x1EB2: 0x02, 0x1EB4: 0x05, 0x1EAA: 0x06, 0x1EF6: 0x14, 0x1EF8: 0x19, 0x1EF4: 0x1E}

def viscii():
    res = {}
    for urow in uni:
        for u in urow:
            if u == 0:
                continue
            r = subprocess.run(["iconv", "-f", "UTF-8", "-t", "VISCII"], input=chr(u).encode(), capture_output=True)
            assert r.returncode == 0, hex(u)
            res[u] = list(r.stdout) or [VISCII_CONTROL[u]]
    return res

def emit(name, table):
    print(f"    static let {name}: [(UInt16, [UInt8])] = [")
    for u in sorted(table):
        print(f"        (0x{u:04X}, [{', '.join('0x%02X' % b for b in table[u])}]),")
    print("    ]\n")

print("// Sinh tự động bởi tools/gen_charsets.py — không sửa tay.")
print("enum CharsetTables {")
emit("tcvn3", build(1, False))
emit("vniWindows", build(2, True))
emit("cp1258", build(4, True))
emit("viscii", viscii())
print("}")
