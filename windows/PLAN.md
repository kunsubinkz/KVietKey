# KVietKey cho Windows — Kế hoạch triển khai

Mục tiêu: bản Windows có tính năng và hành vi gõ giống bản macOS ([../macos](../macos)). Có bộ cài `.exe` (NSIS) và bản portable `.zip` cho **x64 + ARM64**. Toàn bộ việc build và đóng gói chạy trên macOS.

## 0. Trạng thái thực hiện

Cập nhật: giai đoạn 0–6 đã làm xong và build được; giai đoạn 7 (kiểm thử trên Windows thật) **chưa chạy** vì chưa có máy Windows. Hướng dẫn dùng và build: [README.md](README.md).

| Giai đoạn | Trạng thái | Kiểm chứng được tới đâu |
|---|---|---|
| 0. Chuẩn bị | Xong | `global.json` ghim .NET 10, `makensis` cài qua Homebrew |
| 1. Test vector từ Swift | Xong | `KVK_EXPORT_VECTORS=… swift test` → `tests/vectors/` (≈1,2 MB, 12 file) |
| 2. Engine C# | Xong | 80 test xanh; mọi vector Swift (gõ, bảng mã, gõ tắt, chính tả) khớp 100% |
| 3. Core | Xong | 54 test xanh (cài đặt, `InputController` với nền tảng giả, đa luồng) |
| 4. Lớp Windows | Viết xong, **chưa chạy trên Windows** | Biên dịch được; kiểm tra bố cục struct P/Invoke bằng test; hook/SendInput/UI Automation chưa thử thật |
| 5. Giao diện Avalonia | Xong | `--render-ui` xuất PNG trên macOS, đối chiếu với thiết kế; tray/hook chưa thử trên Windows |
| 6. Đóng gói | Xong | `./build_release.sh` ra 2 bộ cài + 2 ZIP + SHA256 trong ~75 s; chưa cài thử trên Windows |
| 7. Kiểm thử Windows | **Chưa làm** | Có sẵn `tools/vm/Test-Typing.ps1` và `KVietKey.exe --selftest`, cần chạy trong VM |

Khác với kế hoạch ban đầu:

- Avalonia dùng **11.3.23** (bản 12 đã ra nhưng 11.x ổn định hơn với code đã viết).
- Hook bàn phím chạy trên luồng riêng cùng bộ theo dõi foreground; có watchdog cài lại hook mỗi 30 s khi đang rảnh.
- Khi focus đổi, ô mới được coi là nhạy cảm cho tới khi UI Automation trả lời (vài chục ms) để không biến đổi nhầm những phím đầu của mật khẩu.
- Phím tắt chỉ gồm phím bổ trợ phải có **ít nhất hai phím** (vd. Ctrl + Shift); một phím bổ trợ đơn lẻ không được nhận.
- `EngineConfig.BackspacePerCodePoint` (bật tự động với Unicode tổ hợp) đã làm để xử lý rủi ro Backspace ở mục 4, giai đoạn 7; cần xác nhận hành vi thực tế trên Notepad/Word/Chrome.
- Chưa làm: chạy KVietKey quyền admin qua Task Scheduler (chỉ cảnh báo khi app foreground chạy quyền cao); `--notepad-test` dạng lệnh của app (thay bằng script PowerShell); phát hiện IME không phải Latin.
- Bộ cài ~50 MB vì self-contained + Avalonia/Skia. Có thể giảm bằng trimming hoặc framework-dependent nếu cần.

## 1. Quyết định công nghệ

| Hạng mục | Lựa chọn |
|---|---|
| Ngôn ngữ / runtime | C# trên .NET 10 (LTS), `Nullable` + `TreatWarningsAsErrors` |
| Engine | Port `VietEngine` (Swift) sang class library C# riêng, không phụ thuộc nền tảng |
| Kiểm chứng engine | Test vector JSON xuất từ bộ test Swift, chạy lại bằng xUnit |
| Giao diện | Avalonia UI 11 (Fluent theme tùy biến, compiled bindings). Không dùng WPF, để dựng và xem trước UI được trên macOS |
| Lớp Windows | P/Invoke (`LibraryImport`, source-generated): `WH_KEYBOARD_LL`, `SendInput`, `SetWinEventHook`, tray icon của Avalonia |
| Đóng gói | `dotnet publish -r win-x64` / `win-arm64`, self-contained, single-file, ReadyToRun → NSIS (`makensis` từ Homebrew) + `.zip` |
| Ký số (tùy chọn) | `osslsigncode` hoặc `jsign` (Authenticode), chạy được trên macOS |

> NativeAOT không biên dịch chéo từ macOS sang Windows được, nên dùng self-contained + ReadyToRun. Nếu sau này có CI Windows (GitHub Actions `windows-latest`) thì có thể chuyển sang NativeAOT để khởi động nhanh hơn.

## 2. Ánh xạ thành phần macOS → Windows

| macOS ([../macos/App](../macos/App)) | Windows | Ghi chú |
|---|---|---|
| `Packages/VietEngine` (~1.500 dòng) | `KVietKey.Engine` | Port 1:1, giữ nguyên tên kiểu/hàm để dễ đối chiếu |
| `tools/gen_charsets.py` → `CharsetTables.swift` | Thêm đầu ra `CharsetTables.g.cs` | Không port tay 551 dòng bảng mã |
| `Input/EventTapManager.swift` (CGEventTap) | `KeyboardHook` (`SetWindowsHookEx(WH_KEYBOARD_LL)`) | Chạy trên luồng riêng có message loop |
| `Input/EventSender.swift` (CGEvent Unicode) | `InputSender` (`SendInput` + `KEYEVENTF_UNICODE`, `VK_BACK`) | Đánh dấu sự kiện tự tạo bằng `dwExtraInfo = 0x4B564B31` |
| `Input/KeyCodeMap.swift` (mã phím Mac) | `VirtualKeys` (VK_*) | Backspace, Tab, Enter, mũi tên, Home/End, PgUp/PgDn, Delete, numpad |
| `InputController.typedCharacter` (`keyboardGetUnicodeString`) | `ToUnicodeEx` với layout của luồng foreground, cờ `0x4` (không làm hỏng trạng thái dead key) | |
| `AppMonitor.swift` (NSWorkspace, bundle ID) | `ForegroundMonitor` (`SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`) → tên `.exe` | Khóa quy tắc ứng dụng là tên exe viết thường, vd. `chrome.exe` |
| `isFrontmostAppFullscreen` | `SHQueryUserNotificationState` + so sánh rect cửa sổ với màn hình | |
| `IsSecureEventInputEnabled` (ô mật khẩu) | UI Automation `IsPassword` của phần tử đang focus (có cache, timeout ngắn) | Windows không có Secure Input toàn cục |
| `AppCompat` (bundle ID trình duyệt/Electron) | Danh sách exe: `chrome.exe`, `msedge.exe`, `firefox.exe`, `brave.exe`, `opera.exe`, `vivaldi.exe`, `Code.exe`, `slack.exe`, `Discord.exe`, `Notion.exe`, `ms-teams.exe`, `Figma.exe`… | |
| `selectionLength` (AX) | UIA `TextPattern.GetSelection` | Cho tính năng sửa lỗi autocomplete trên thanh địa chỉ |
| `AccessibilityGuard` | Không cần quyền. Thay bằng cảnh báo khi app foreground chạy quyền admin (UIPI chặn) | |
| `LaunchAtLogin` (SMAppService) | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | |
| `Preferences` (UserDefaults + `macros.json`) | `%APPDATA%\KVietKey\settings.json` + `macros.json` | Cùng schema JSON với bản Mac, xuất/nhập qua lại được |
| `StatusBarController` + `UI/MenuPanel.swift` | Avalonia `TrayIcon` + cửa sổ popup không viền đặt cạnh khay | Icon chữ "V"/"E" vẽ động |
| `UI/Onboarding.swift` | Onboarding 3 bước (bỏ bước cấp quyền, thay bằng bước "tắt phím Ctrl+Shift đổi ngôn ngữ của Windows") | |
| `Settings/SettingsView.swift` (6 mục) | `SettingsWindow` với 6 trang: Cơ bản, Gõ tiếng Việt, Ứng dụng, Gõ tắt, Nâng cao, Giới thiệu | |
| `UI/Theme.swift` | `Themes/KVietKey.axaml` (màu navy, bo góc, Be Vietnam Pro nhúng sẵn, giấy phép OFL) | Theo [KVietKey — Giao diện.html](<../KVietKey — Giao diện.html>) |
| `--selftest`, `--render-ui` | `--selftest` (Windows), `--render-ui` (Avalonia.Headless, chạy được trên macOS) | |
| `--textedit-test`, `tools/stress` | `--notepad-test` + script PowerShell chạy trong VM | |
| `NSSound` | `PlaySound` / `SystemSounds` | |

## 3. Cấu trúc thư mục

```
windows/
  PLAN.md
  KVietKey.slnx
  Directory.Build.props            Phiên bản đọc từ ../VERSION, cấu hình chung
  global.json                      Ghim SDK .NET 10
  src/
    KVietKey.Engine/               net10.0, port VietEngine (không phụ thuộc gì)
    KVietKey.Core/                 net10.0: Settings, Preferences, AppRule, Hotkey, InputController,
                                   interface IKeyboardHook / IInputSender / IForegroundMonitor / IPlatform
    KVietKey.Platform.Windows/     net10.0, [SupportedOSPlatform("windows")]: P/Invoke, hook, SendInput, UIA, registry
    KVietKey.Platform.Fake/        Nền tảng giả để chạy UI trên macOS (không hook, gõ mô phỏng trong ô test)
    KVietKey.App/                  Avalonia: tray, menu panel, onboarding, settings, đa ngôn ngữ vi/en
  tests/
    KVietKey.Engine.Tests/         xUnit: chạy test vector + test riêng của C#
    KVietKey.Core.Tests/           InputController với hook/sender giả, Preferences, Hotkey
    vectors/                       *.json xuất từ bộ test Swift (commit vào repo)
  installer/
    kvietkey.nsi                   NSIS: cài per-user, shortcut, Run-at-login, gỡ cài đặt
    assets/                        icon .ico, ảnh banner
  tools/
    gen_charsets_cs.py (hoặc mở rộng ../macos/tools/gen_charsets.py)
    vm/                            Script PowerShell kiểm thử trong máy ảo Windows
  build_release.sh                 test → publish x64/arm64 → ký → NSIS → zip
```

## 4. Các giai đoạn

### Giai đoạn 0 — Chuẩn bị (0,5 ngày)
- Cài trên macOS: `.NET 10 SDK`, `brew install makensis osslsigncode`.
- Chuẩn bị máy ảo Windows 11 ARM (UTM/Parallels) để kiểm thử. Windows ARM chạy được cả bản x64 qua giả lập.
- Tạo solution, `Directory.Build.props`, `global.json`, `.gitignore` (`bin/`, `obj/`, `artifacts/`).

### Giai đoạn 1 — Xuất test vector từ Swift (1 ngày)
Mục tiêu: một nguồn sự thật duy nhất cho hành vi engine.
- Sửa `EngineTestCase.check` ([Typist.swift](../macos/Packages/VietEngine/Tests/VietEngineTests/Typist.swift)): khi có biến môi trường `KVK_EXPORT_VECTORS=<thư mục>`, ghi lại mỗi ca `{config, macros, input, expected}`. Ghi file khi kết thúc (dùng `XCTestObservation`), mỗi lớp test ra một file JSON. Không đặt biến thì hành vi test giữ nguyên.
- `CorpusTests`: xuất danh sách từ và chuỗi phím đã sinh cho từng kiểu gõ (Telex/VNI/VIQR/kết hợp).
- Xuất thêm vector bảng mã: `encode`/`decode` cho cả 6 bảng mã trên toàn bộ ký tự tiếng Việt.
- Định dạng:
  ```json
  { "suite": "TelexTests.testWords",
    "config": { "method": "telex", "charset": "unicode", "modernTone": true, "...": "..." },
    "macros": {},
    "cases": [ { "input": "tieengs vieetj", "expected": "tiếng việt" } ] }
  ```
- Lệnh: `cd macos/Packages/VietEngine && KVK_EXPORT_VECTORS=../../../windows/tests/vectors swift test`.

### Giai đoạn 2 — Port engine sang C# (3–4 ngày)
- Port theo thứ tự: `Types` → `InputMethod` → `ToneMarker` → `SpellChecker` → `Macro` → `Charset` (+ bảng sinh tự động) → `Engine`.
- Lưu ý khác biệt Swift ↔ C#:
  - `Character` của Swift là grapheme. Trong C# dùng `string`/`Rune`, đếm "ký tự trên màn hình" bằng `StringInfo` (text element).
  - `decomposedStringWithCanonicalMapping` / `precomposed…` → `Normalize(FormD/FormC)`.
  - `isLetter`, `isUppercase`, `lowercased()` → `Rune.IsLetter`, `ToLowerInvariant` (không phụ thuộc culture, tránh lỗi kiểu tiếng Thổ Nhĩ Kỳ).
  - So sánh chuỗi luôn dùng `StringComparison.Ordinal`, chuẩn hóa NFC trước khi so.
- Port `Typist` sang C# và chạy toàn bộ vector.
- **Xong khi:** 100% vector pass. Engine xử lý một phím < 50 µs (đo bằng BenchmarkDotNet), không cấp phát trong đường nóng nếu làm được.

### Giai đoạn 3 — Core (2 ngày)
- `Settings` với schema JSON trùng bản Mac. Thiếu khóa thì dùng mặc định (giống `init(from:)`); khóa chỉ dành cho Mac thì bỏ qua khi đọc.
- `Hotkey`: modifier dùng bitmask riêng (Ctrl/Shift/Alt/Win), không phụ thuộc `CGEventFlags`. Hiển thị dạng `Ctrl + Shift`.
- `AppMode`/`AppRule` theo tên exe. Giữ nguyên logic `rememberAppState`, `lastAppState`.
- `InputController`: port logic từ [InputController.swift](../macos/App/Input/InputController.swift), chỉ phụ thuộc interface:
  - Hotkey chỉ gồm phím bổ trợ (bấm rồi nhả Ctrl+Shift mà không có phím nào khác) hoặc tổ hợp bổ trợ + phím.
  - Có Ctrl/Alt/Win → reset rồi cho qua. Backspace → `engine.backspace()`. Phím điều hướng hoặc click chuột → reset. Tab/Enter → `processBreak`.
  - Áp dụng `EngineResult`: gửi Backspace + chuỗi, `passthrough` thì gửi lại phím gốc.
- Test bằng hook/sender giả, mô phỏng màn hình như `Typist`.

### Giai đoạn 4 — Lớp Windows (4–5 ngày)
- **KeyboardHook**: một luồng riêng chạy `SetWindowsHookEx(WH_KEYBOARD_LL)` + `GetMessage`. Callback phải trả về nhanh (Windows tự gỡ hook nếu quá `LowLevelHooksTimeout`). Bỏ qua sự kiện có `LLKHF_INJECTED` + `dwExtraInfo == magic`. Trả `1` để nuốt phím.
- **Mouse**: `WH_MOUSE_LL` (chỉ lấy sự kiện nhấn nút) để reset engine, tương đương `leftMouseDown` trên Mac.
- **InputSender**: gom Backspace và chuỗi Unicode vào một lần gọi `SendInput` (nguyên tử hơn CGEvent). Tách chuỗi khi cần (cặp surrogate gửi liền nhau). Hỗ trợ `delayMicros` cho Electron: tách nhiều lần `SendInput` + chờ ngắn.
- **Ký tự gõ ra**: `ToUnicodeEx(vk, scan, keyState, buf, 8, 0x4, GetKeyboardLayout(foregroundThread))`. Bỏ ký tự điều khiển (< 0x20, 0x7F).
- **ForegroundMonitor**: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` → `GetWindowThreadProcessId` → `QueryFullProcessImageName`. Bỏ qua cửa sổ của chính KVietKey.
- **Ô mật khẩu**: hỏi UIA ở `EVENT_OBJECT_FOCUS` (không hỏi trong callback hook), lưu kết quả `IsPassword` vào cờ.
- **Toàn màn hình**: `SHQueryUserNotificationState` (`QUNS_BUSY`, `QUNS_RUNNING_D3D_FULL_SCREEN`, `QUNS_PRESENTATION_MODE`).
- **App chạy quyền admin**: phát hiện qua integrity level của process foreground. Hiện cảnh báo trên menu panel và cho tùy chọn "Chạy KVietKey với quyền admin" (Task Scheduler, `RunLevel=Highest`, khởi động cùng Windows).
- **Chạy một phiên bản duy nhất**: named mutex `Local\KVietKey`. Lần chạy thứ hai thì mở cửa sổ cài đặt của phiên bản đang chạy (named pipe).
- **Xung đột**: phát hiện process `UniKeyNT.exe`, `EVKey64.exe`, `OpenKey64.exe`… Phát hiện bàn phím Vietnamese Telex/IME tích hợp của Windows (`GetKeyboardLayout`), khi IME không phải Latin (Nhật, Trung…) đang bật thì tạm dừng.
- **Ctrl+Shift**: Windows mặc định dùng tổ hợp này để đổi bàn phím. Onboarding hướng dẫn tắt hoặc đề xuất đổi phím tắt.
- **Bảng mã cũ (TCVN3, VNI, VISCII, CP1258)**: gửi từng byte dưới dạng ký tự U+00xx qua `KEYEVENTF_UNICODE`, giống cách UniKey làm. Cần kiểm tra thực tế với font `.VnTime`/`VNI-Times` trong Word/Notepad.

### Giai đoạn 5 — Giao diện Avalonia (4–5 ngày)
- Theme theo bản thiết kế: sidebar navy, card bo góc, công tắc, phông Be Vietnam Pro nhúng.
- **Menu panel** (bấm trái icon khay): kiểu gõ, bảng mã, 3 công tắc nhanh, Gõ tắt / Cài đặt / Thoát. Bấm phải: menu ngữ cảnh rút gọn.
- **Onboarding** 3 bước.
- **Cài đặt** 6 trang. Trang Ứng dụng liệt kê các process đang có cửa sổ (icon lấy từ exe).
- **Gõ tắt**: bảng, nhập/xuất `.txt` theo cùng định dạng `viết_tắt:nội dung`.
- Đa ngôn ngữ: hàm `tr(vi, en)` như bản Mac, `AppLanguage.Auto` theo `CultureInfo.CurrentUICulture`.
- `--render-ui <thư mục>` chạy được trên macOS bằng Avalonia.Headless để so ảnh với bản Mac và bản thiết kế.
- Hỗ trợ DPI cao (per-monitor v2), Dark/Light theo hệ thống (tùy chọn), Windows 10 1809+ và Windows 11.

### Giai đoạn 6 — Đóng gói (1–2 ngày)
- `build_release.sh`:
  1. `dotnet test` (engine + core)
  2. `dotnet publish src/KVietKey.App -c Release -r win-x64|win-arm64 --self-contained -p:PublishSingleFile=true -p:PublishReadyToRun=true`
  3. Ký `KVietKey.exe` (nếu đặt `SIGN_CERT`/`SIGN_PASSWORD`, dùng `osslsigncode`)
  4. `makensis -DARCH=x64|arm64 -DVERSION=$(cat ../VERSION) installer/kvietkey.nsi`
  5. Nén portable: `KVietKey-<ver>-win-<arch>.zip`
  6. Ra thư mục `artifacts/`: `KVietKey-<ver>-win-x64-setup.exe`, `…-arm64-setup.exe`, 2 bản `.zip`, `SHA256SUMS`
- **NSIS**: mặc định cài per-user vào `%LOCALAPPDATA%\Programs\KVietKey` (không cần admin). Gồm shortcut Start Menu, tùy chọn khởi động cùng Windows, đăng ký mục Gỡ cài đặt trong `HKCU`, và đóng KVietKey đang chạy trước khi cài đè hoặc gỡ.
- **Portable**: chạy trực tiếp. Nếu có file `portable.txt` cạnh exe thì lưu cài đặt ngay trong thư mục đó.
- Không ký số thì SmartScreen sẽ cảnh báo. Ghi rõ trong hướng dẫn, khuyến nghị mua chứng chỉ OV/EV hoặc dùng Azure Trusted Signing khi phát hành chính thức.

### Giai đoạn 7 — Kiểm thử trên Windows (3 ngày, song song với giai đoạn 5–6)
- `--selftest`: engine qua đường `ToUnicodeEx` → engine → màn hình giả lập, lưu/đọc cài đặt, tách chuỗi, hotkey, khả năng cài hook.
- `--notepad-test`: tự mở Notepad, gõ `tools/stress/text.txt` bằng `SendInput` rồi so nội dung (tương tự `--textedit-test` của Mac).
- Ma trận thủ công (cả Telex và VNI, bảng mã Unicode):

  | Nhóm | Ứng dụng |
  |---|---|
  | Win32/UWP | Notepad (Win 11 mới), WordPad/Word, Explorer (đổi tên file), hộp Run |
  | Trình duyệt | Chrome, Edge, Firefox: ô nhập, thanh địa chỉ (autocomplete), Google Docs |
  | Electron | VS Code, Slack, Discord, Teams, Notion |
  | Terminal | Windows Terminal, cmd/conhost, PowerShell |
  | Khác | Zalo, Telegram, Excel (sửa ô), game toàn màn hình (phải tự tạm dừng), ô mật khẩu |
  | Hệ thống | App chạy quyền admin, đổi layout bàn phím, IME Nhật, nhiều màn hình, DPI 150% |

- **Rủi ro cần đo sớm:** ở chế độ **Unicode tổ hợp**, Backspace của Windows thường chỉ xóa một code point (dấu tổ hợp), còn macOS xóa cả grapheme. Cần quy định số Backspace theo nền tảng: thêm tùy chọn trong `EngineConfig` hoặc quy đổi ở `InputSender` bằng cách tính lại số code point của đoạn bị xóa.

## 5. Thứ tự và mốc

| Mốc | Nội dung | Ước lượng |
|---|---|---|
| M1 | Giai đoạn 0–2: engine C# pass 100% vector, chạy `dotnet test` trên macOS | ~5 ngày |
| M2 | Giai đoạn 3–4: gõ được tiếng Việt trong Notepad/Chrome trên VM (chưa có UI, cấu hình bằng JSON) | ~6 ngày |
| M3 | Giai đoạn 5: đủ UI, xem trước được trên macOS | ~5 ngày |
| M4 | Giai đoạn 6–7: bộ cài x64/arm64, qua hết ma trận kiểm thử → phát hành 1.0.0 | ~4 ngày |

## 6. Rủi ro và cách xử lý

| Rủi ro | Cách xử lý |
|---|---|
| Hook bị Windows gỡ khi callback chậm | Không gọi UIA/IO trong callback. Có watchdog cài lại hook. Đo thời gian callback ở chế độ debug |
| UIPI: không gõ được vào app chạy quyền admin | Cảnh báo + tùy chọn chạy quyền cao qua Task Scheduler |
| Phím Ctrl+Shift xung đột với hệ thống | Onboarding hướng dẫn tắt, gợi ý phím tắt khác |
| Engine C# lệch hành vi so với Swift | Test vector dùng chung. Mỗi lỗi mới thêm test vào Swift trước rồi xuất lại vector |
| Autocomplete trình duyệt nuốt Backspace | Bật `browserAutocompleteFix`: kiểm tra vùng chọn qua UIA, gửi `Delete` trước |
| Electron mất ký tự khi gửi nhanh | `electronDelayMicros` theo danh sách exe, chỉnh được trong Nâng cao |
| Antivirus/SmartScreen nghi ngờ keyboard hook | Ký số, mã nguồn mở, không có mã mạng. Ghi chú trong README |
| Avalonia tray trên Windows thiếu tính năng | Nếu cần thì tự dùng `Shell_NotifyIcon` qua P/Invoke |

## 7. Ngoài phạm vi 1.0
- Chế độ TSF (Text Services Framework) như một IME thật (tương đương InputMethodKit v2 của bản Mac).
- Cập nhật tự động, winget/Scoop manifest, Microsoft Store (MSIX).
- Đồng bộ cài đặt giữa máy Mac và Windows (hiện chỉ xuất/nhập JSON thủ công).
