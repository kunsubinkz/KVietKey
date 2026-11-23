# KVietKey cho Windows

Bộ gõ tiếng Việt cho Windows 10/11 (x64 và ARM64), cùng engine và hành vi với [bản macOS](../macos). Viết bằng C# trên .NET 10, giao diện Avalonia, không cần quyền admin để cài và dùng. Kế hoạch và trạng thái chi tiết: [PLAN.md](PLAN.md).

> **Trạng thái:** đã build và đóng gói được, engine kiểm chứng đầy đủ bằng test vector từ bản Swift. Phần chạy trên Windows thật (hook bàn phím, SendInput, khay hệ thống, bộ cài) **chưa được thử** vì mới build trên macOS. Hãy chạy kiểm thử ở mục "Kiểm thử trên Windows" trước khi phát hành.

## Cấu trúc

```
src/
  KVietKey.Engine/            Engine gõ tiếng Việt (port 1:1 từ VietEngine), không phụ thuộc nền tảng
  KVietKey.Core/              Cài đặt, InputController, giao diện trừu tượng cho nền tảng
  KVietKey.Platform.Windows/  P/Invoke: hook bàn phím/chuột, SendInput, UI Automation, WinEvent, registry
  KVietKey.Platform.Fake/     Nền tảng giả để test và chạy giao diện không cần Windows
  KVietKey.App/               Ứng dụng Avalonia: khay, panel, cài đặt, chào mừng; --selftest, --render-ui
tests/
  KVietKey.Engine.Tests/      Chạy lại test vector JSON do bộ test Swift xuất ra
  KVietKey.Core.Tests/        Cài đặt, InputController (nền tảng giả), bố cục struct P/Invoke
  KVietKey.App.Tests/         CLI, single-instance, ánh xạ phím
  vectors/                    Test vector (sinh từ Swift, đã commit)
installer/kvietkey.nsi        Bộ cài NSIS (cài theo người dùng, %LOCALAPPDATA%\Programs\KVietKey)
tools/                        gen_charsets_cs.py, make_ico.py, vm/Test-Typing.ps1
build_release.sh              Test → publish x64/arm64 → ký (tùy chọn) → NSIS + ZIP + SHA256
```

## Build và test (trên macOS/Linux)

Cần .NET 10 SDK, `makensis` (`brew install makensis`), `zip`.

```sh
cd windows
dotnet test KVietKey.slnx                  # engine + core + app
./build_release.sh                         # artifacts/release/: 2 bộ cài + 2 ZIP portable + SHA256SUMS
./build_release.sh --arch x64 --skip-tests # chỉ x64, bỏ qua test
dotnet run --project src/KVietKey.App -- --render-ui /tmp/kvk-ui   # xuất ảnh PNG các màn hình
dotnet run --project src/KVietKey.App -- --selftest                # smoke test (trên macOS chạy phần dùng chung)
```

Ký số Authenticode (tùy chọn, cần `brew install osslsigncode`): `SIGN_PFX=cert.pfx SIGN_PASSWORD=… ./build_release.sh`. Không ký thì SmartScreen sẽ cảnh báo khi cài.

## Test vector từ bản Swift

Bản C# phải gõ giống hệt bản Swift. Bộ test Swift ghi lại mọi ca gõ (kèm cấu hình), bảng mã, gõ tắt và kiểm tra chính tả ra JSON:

```sh
cd macos/Packages/VietEngine
KVK_EXPORT_VECTORS=../../../windows/tests/vectors swift test
cd ../../../windows && dotnet test tests/KVietKey.Engine.Tests
```

Khi sửa engine, sửa ở Swift trước, chạy lại lệnh trên rồi sửa C# cho tới khi xanh. Bảng mã C# sinh từ bảng Swift: `python3 tools/gen_charsets_cs.py > src/KVietKey.Engine/CharsetTables.g.cs`.

## Dữ liệu người dùng

- Cài đặt: `%APPDATA%\KVietKey\settings.json`, gõ tắt: `%APPDATA%\KVietKey\macros.json` (cùng định dạng với bản macOS; riêng `hotkey` và khóa của `appRules`/`lastAppState` khác vì Windows dùng tên file exe như `chrome.exe`).
- Bản portable: nếu có `portable.txt` cạnh `KVietKey.exe` thì lưu trong thư mục `data\` cạnh exe.
- Tham số dòng lệnh: `--autostart`, `--allow-multiple`, `--data-dir <thư mục>`, `--trace-file <file>` (ghi vết, có chứa ký tự gõ), `--selftest`, `--render-ui <thư mục>`.

## Kiểm thử trên Windows

Làm trong máy ảo Windows 10/11 (x64 và ARM64), không làm trên máy đang dùng.

1. Cài bằng `KVietKey-<ver>-win-<arch>-setup.exe`, chạy `KVietKey.exe --selftest` (phải ra `SELFTEST OK`, gồm cả việc cài được hook).
2. `powershell -STA -File tools\vm\Test-Typing.ps1` rồi thêm `-Method vni`: tự mở Notepad, gõ và so kết quả.
3. Làm thủ công theo ma trận ở [PLAN.md](PLAN.md) (mục giai đoạn 7): Notepad, Word, Chrome/Edge/Firefox (cả thanh địa chỉ), VS Code/Slack/Discord/Teams, Windows Terminal, ô mật khẩu, app chạy quyền admin, game toàn màn hình, nhiều màn hình, DPI 150%.
4. Chú ý riêng: bảng mã **Unicode tổ hợp** (Backspace theo code point), bảng mã cũ (TCVN3/VNI) với phông tương ứng, và phím tắt Ctrl + Shift trùng phím đổi bàn phím của Windows.

## Giới hạn đã biết

- Ứng dụng chạy quyền admin không nhận được phím từ KVietKey (UIPI của Windows). Panel và khay sẽ báo khi app phía trước chạy quyền cao.
- Ứng dụng UWP chạy trong `ApplicationFrameHost.exe` sẽ dùng chung một quy tắc theo tên exe.
- Phông Be Vietnam Pro (SIL OFL 1.1) được nhúng; giấy phép nằm trong `licenses\`.
