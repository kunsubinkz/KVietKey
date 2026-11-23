# KVietKey

Bộ gõ tiếng Việt cho macOS (Apple Silicon, viết bằng Swift) và Windows 10/11 (x64, ARM64, viết bằng C#). Phiên bản 1.0.0. Tác giả: kunsubin.
Hướng dẫn sử dụng có hình ảnh: [KVietKey-Huong-dan-su-dung.docx](KVietKey-Huong-dan-su-dung.docx).

| Nền tảng | Thư mục | Ghi chú |
|---|---|---|
| macOS | [macos/](macos) | Bản gốc, đã hoàn chỉnh |
| Windows | [windows/](windows) | Đã build và đóng gói được; chưa kiểm thử trên Windows thật, xem [windows/README.md](windows/README.md) |

Hai bản dùng chung hành vi gõ: bộ test Swift xuất test vector JSON để bản C# chạy lại (xem hướng dẫn trong windows/README.md).

- Kiểu gõ: Telex, VNI, VIQR, Telex + VNI (gõ dấu sau từ được: nhapaj → nhập, dudowcj → được). Bảng mã: Unicode dựng sẵn/tổ hợp, TCVN3, VNI Windows, VISCII, CP1258.
- Đặt dấu kiểu cũ/mới, bỏ dấu tự do, kiểm tra chính tả, tự khôi phục từ tiếng Anh, gõ tắt, viết hoa đầu câu.
- Cài đặt theo từng ứng dụng, tự tạm dừng ở ô mật khẩu và app toàn màn hình.
- Không có mã mạng; không lưu hay gửi nội dung gõ.

Phần còn lại của tài liệu này nói về bản macOS (chạy lệnh trong thư mục `macos/`).

## Cấu trúc

```
macos/                          Bản macOS
  Packages/VietEngine/          Swift package thuần (engine + unit test), không phụ thuộc AppKit
  App/                          Ứng dụng menu bar (CGEventTap, SwiftUI cài đặt, onboarding)
  Resources/                    Info.plist, AppIcon.icns
  tools/                        gen_charsets.py (bảng mã), make_icon.swift (icon)
  build_release.sh              Test → build → .app → ký → ZIP/DMG → (notarize)
VERSION                         Phiên bản dùng chung cho mọi nền tảng
```

## Giao diện

Theo bản thiết kế `KVietKey — Giao diện.html`: panel menu bar gọn (kiểu gõ, bảng mã, 3 công tắc nhanh, Gõ tắt/Cài đặt/Thoát), onboarding 3 bước, cài đặt 6 mục với sidebar xanh navy. Phông Be Vietnam Pro được dùng nếu máy đã cài, nếu không dùng phông hệ thống.
`KVietKey --render-ui <thư mục>` xuất ảnh PNG các màn hình để đối chiếu (không cần quyền màn hình; ô nhập/menu thả xuống của AppKit hiện thành ô vàng trong ảnh xuất).

## Build & test

```sh
cd macos
cd Packages/VietEngine && swift test && cd ../..   # unit test engine
./build_release.sh                        # build/release/KVietKey.app + .zip + .dmg (arm64, ký ad-hoc)
./build_release.sh --universal            # arm64 + x86_64
build/release/KVietKey.app/Contents/MacOS/KVietKey --selftest   # smoke test không cần quyền
```

Phát hành chính thức: `SIGN_IDENTITY="Developer ID Application: …" NOTARY_PROFILE=<profile> ./build_release.sh`.

## Cài đặt & dùng

Mở `KVietKey.app` (bản ad-hoc: chuột phải → Open), cấp quyền **Accessibility** khi được hỏi. Phím tắt mặc định chuyển Việt ⇄ Anh: ⌃⇧.
Bản ký ad-hoc phải cấp lại quyền sau mỗi lần build mới.

## Bản quyền

© 2026 kunsubin. Phát hành theo giấy phép MIT (xem [LICENSE](LICENSE)).

## Ngoài phạm vi hiện tại

Sparkle (cập nhật tự động), Homebrew cask, trang web, chế độ InputMethodKit (v2).
