# Lộ trình tính năng KVietKey

Các tính năng đề xuất sau bản 1.0.0. Mỗi tính năng khi bắt đầu làm sẽ có spec riêng trong [`docs/features/`](features/).

| # | Tính năng | Ưu tiên | Phạm vi chính | Spec |
|---|---|---|---|---|
| 1 | ESC khôi phục phím gốc | Cao | Engine, InputController | [esc-restore-va-sua-tu-truoc.md](features/esc-restore-va-sua-tu-truoc.md) |
| 2 | Xoá lùi qua dấu cách để sửa từ trước | Cao | Engine | [esc-restore-va-sua-tu-truoc.md](features/esc-restore-va-sua-tu-truoc.md) |
| 3 | Gõ nhanh (Quick Telex): `cc`→ch, `gg`→gi, `kk`→kh, `nn`→ng, `qq`→qu, `pp`→ph, `tt`→th | Cao | InputMethod, Engine | — |
| 4 | Từ điển ngoại lệ cá nhân cho kiểm tra chính tả và tự khôi phục | Cao | SpellChecker, Settings | — |
| 5 | Chuyển mã văn bản đang chọn bằng phím tắt; thêm bỏ dấu, HOA/thường/Hoa Đầu Từ | Vừa | Support (ClipboardConverter), Charset | — |
| 6 | Gõ tắt nâng cao: `{date}`, `{time}`, `{clipboard}`, gõ tắt theo ứng dụng, nhập file UniKey/EVKey | Vừa | Macro, Settings | — |
| 7 | HUD “V”/“E” khi chuyển chế độ | Vừa | UI | — |
| 8 | Phím tắt đổi kiểu gõ / bảng mã | Vừa | InputController, Preferences | — |
| 9 | Xuất/nhập toàn bộ cài đặt | Thấp | Preferences, SettingsView | — |
| 10 | Cập nhật tự động (Sparkle), Homebrew cask | Thấp | Build, phát hành | — |
| 11 | CI tạo GitHub Release khi push tag `v*` | Thấp | `.github/workflows` | — |
| 12 | Chế độ InputMethodKit (v2) | Dài hạn | Kiến trúc mới | — |

## Nguyên tắc chung

- Logic gõ nằm trong `Packages/VietEngine` (không phụ thuộc AppKit) và phải có unit test qua `Typist`.
- Mọi tuỳ chọn mới đều có công tắc trong Cài đặt, có giá trị mặc định khi giải mã để tương thích file cài đặt cũ.
- Giữ cam kết riêng tư: không có mã mạng (trừ Sparkle nếu làm, và phải ghi rõ), không lưu hay gửi nội dung gõ.
