#!/usr/bin/env python3
"""Sinh Assets/kvietkey.ico (và kvietkey.png) từ icon của bản macOS bằng `iconutil` + `sips` (chỉ chạy trên macOS).

Dùng: python3 tools/make_ico.py
Các ảnh PNG được nhúng nguyên trong .ico (Windows Vista trở lên hỗ trợ).
"""
import struct
import subprocess
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parent.parent
icns = (root / "../macos/Resources/AppIcon.icns").resolve()
out_dir = root / "src/KVietKey.App/Assets"
sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]

with tempfile.TemporaryDirectory() as tmp:
    iconset = Path(tmp) / "i.iconset"
    subprocess.run(["iconutil", "-c", "iconset", str(icns), "-o", str(iconset)], check=True)
    source = iconset / "icon_512x512.png"
    images = []
    for size in sizes:
        target = Path(tmp) / f"{size}.png"
        subprocess.run(["sips", "-z", str(size), str(size), str(source), "--out", str(target)],
                       check=True, stdout=subprocess.DEVNULL)
        images.append((size, target.read_bytes()))

    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries, blobs = b"", b""
    for size, data in images:
        entries += struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(data), offset)
        blobs += data
        offset += len(data)
    (out_dir / "kvietkey.ico").write_bytes(header + entries + blobs)
    (out_dir / "kvietkey.png").write_bytes(images[-1][1])
print("OK", [s for s, _ in images])
