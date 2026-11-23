# ESC khôi phục phím gốc & xoá lùi để sửa từ trước

- Nhánh: `feature/esc-restore-backspace-edit`
- Trạng thái: Đề xuất (chưa cài đặt)
- Phạm vi: `Packages/VietEngine` (engine + test), `App/Input/InputController.swift`, `App/Settings/*`

## 1. Bối cảnh

Hai tình huống người dùng gặp hằng ngày mà bản 1.0.0 chưa xử lý:

| Tình huống | Hiện tại | Mong muốn |
|---|---|---|
| Gõ `test` (muốn giữ tiếng Anh) | `tét` — là âm tiết hợp lệ nên *tự khôi phục* không can thiệp | Nhấn **ESC** → `test` |
| Gõ `bas` (tên biến) | `bá` | Nhấn **ESC** → `bas` |
| Gõ `viet ` rồi mới nhớ ra thiếu dấu | Phải xoá cả từ gõ lại; `viet⌫j` cho ra `vietj` vì engine đã reset sau dấu cách | `viet␣` → **⌫** → `j` → `việt` |

Nguyên nhân trong code:

- ESC nằm trong `KeyCode.navigation`, nên `InputController.handleKeyDown` chỉ gọi `engine.reset()` rồi cho phím đi tiếp.
- `Engine.processBreak` gọi `resetWord()` sau mỗi phím ngắt từ; `Engine.backspace()` thoát sớm khi `chars` rỗng (`guard !chars.isEmpty`), nên không có cách nào quay lại từ trước.

## 2. Tính năng A — ESC khôi phục phím gốc

### Hành vi

1. Khi đang có từ dở (`hasPendingWord`) **và** chữ trên màn hình khác chuỗi phím đã gõ (`onScreen != encode(String(raw))`):
   - Thay từ trên màn hình bằng đúng các phím đã gõ (`raw`).
   - **Nuốt phím ESC** (không gửi tới ứng dụng), để không vô tình đóng hộp thoại hay huỷ thao tác.
   - Phần còn lại của từ hiện tại được gõ nguyên văn: các phím tiếp theo **không** biến đổi cho tới phím ngắt từ kế tiếp (ví dụ `test` → ESC → `s` → `tests`, không thành `tét`).
2. Trong mọi trường hợp khác (không có từ dở, hoặc từ chưa bị biến đổi): ESC đi qua bình thường và engine `reset()` như hiện tại.
3. Nếu `rawReliable == false` (người dùng đã ⌫ trong từ, `raw` chỉ còn chữ cái gốc), khôi phục về các chữ cái **không dấu** đang có (`raw`). Như vậy vẫn nhất quán: ESC luôn cho ra chữ không bị biến đổi.

### Thay đổi engine

```swift
// EngineConfig
public var escRestore = true

// Engine — trạng thái mới
private var literalUntilBreak = false

/// Gọi khi người dùng nhấn ESC. Trả về `nil` nếu không có gì để khôi phục (ESC đi qua).
public func restoreRaw() -> EngineResult? {
    guard config.escRestore, !chars.isEmpty else { return nil }
    let target = config.charset.encode(String(raw))
    guard target != onScreen else { return nil }
    let d = diff(from: onScreen, to: target)
    onScreen = target
    literalUntilBreak = true
    return EngineResult(backspaces: d.backspaces, text: d.text, passthrough: false)
}
```

- Trong `process(_:)`: khi `literalUntilBreak` bật thì bỏ qua `config.method.action(for:)`, chỉ `appendLiteral` + `raw.append`, và cập nhật `onScreen` bằng phím vừa gõ (kết quả `.pass`).
- Khi ở chế độ literal, `chars` không còn phản ánh màn hình. Cách gọn nhất: lúc vào literal, dựng lại `chars` từ `raw` (mỗi phím là một `VChar` không dấu, `tone = .none`) để `backspace()`, `processBreak()` vẫn đúng.
- `processBreak`: không chạy *tự khôi phục* và *gõ tắt* cho từ literal (người dùng đã chủ động chọn giữ nguyên). Sau đó `literalUntilBreak = false` trong `resetWord()`.

### Thay đổi app

`InputController.handleKeyDown`, **trước** nhánh `KeyCode.navigation`:

```swift
if keyCode == KeyCode.escape, let result = engine.restoreRaw() {
    return emit(result, event: event, proxy: proxy)   // passthrough=false → ESC bị nuốt
}
```

## 3. Tính năng B — Xoá lùi để sửa từ trước

### Hành vi

1. Khi người dùng gõ **dấu cách** để kết thúc một từ và từ đó *không* bị thay thế (không phải gõ tắt, không bị tự khôi phục), engine lưu lại trạng thái của từ đó.
2. Nếu phím **kế tiếp** là ⌫ (xoá đúng dấu cách vừa gõ), engine nạp lại trạng thái đã lưu: từ trước trở thành từ đang gõ, gõ tiếp phím dấu sẽ áp vào nó.
   - `viet␣⌫j` → `việt`
   - `tieng␣⌫s` → `tiếng`
   - `toi␣⌫f` → `tòi`
3. Chỉ giữ **một mức**: chỉ sửa được từ ngay trước dấu cách vừa xoá. Xoá tiếp vào trong từ thì dùng logic `backspace()` sẵn có.
4. Bản lưu bị huỷ khi có bất kỳ sự kiện nào khác: gõ phím khác, click chuột, phím điều hướng, đổi ứng dụng, ⌘/⌃/⌥ + phím, `reset()`.

### Vì sao chỉ áp dụng cho dấu cách

- Enter/Tab thường mang nghĩa “gửi”, “chuyển ô”: con trỏ có thể không còn nằm sau từ đó.
- Dấu câu (`.`, `,`) có thể đã được ứng dụng tự sửa hoặc tự thêm dấu cách; khó chắc chắn về nội dung màn hình.

### Thay đổi engine

```swift
// EngineConfig
public var editPreviousWord = true

private struct WordSnapshot {
    var chars: [VChar]; var tone: Tone; var raw: [Character]
    var undone: Set<Character>; var rawReliable: Bool; var onScreen: String
    var capState: CapState
}
private var previousWord: WordSnapshot?
```

- `processBreak(_:)`: nếu `config.editPreviousWord`, `input == " "`, `!chars.isEmpty` và `result` là `.pass` (không thay thế) → lưu `previousWord` (gồm cả `capState` **trước** khi `updateCapState` chạy). Ngược lại → `previousWord = nil`.
- `process(_:)` và `reset()`: đặt `previousWord = nil` (phải làm ở đầu `process`, trước mọi xử lý).
- `backspace()`:

  ```swift
  if chars.isEmpty {
      if let snap = previousWord { restore(snap) }   // dấu cách vừa bị app xoá
      previousWord = nil
      return
  }
  ```

  Lưu ý thứ tự: dấu cách đi qua `process(" ")` → `processBreak(" ")`. `process` xoá `previousWord` ở đầu hàm, sau đó `processBreak` mới lưu bản mới, nên không bị mất. Sau `processBreak` thì `chars` luôn rỗng, nên ⌫ ngay sau dấu cách chắc chắn vào nhánh `chars.isEmpty`.
- `CapState` hiện là `private enum` ở cấp file — giữ nguyên, `WordSnapshot` cũng private trong cùng file.

### Thay đổi app

Không cần thay đổi logic sự kiện: `InputController` đã gọi `engine.backspace()` và cho ⌫ đi tiếp. Các đường `engine.reset()` sẵn có (click chuột, điều hướng, đổi app, phím bổ trợ) tự huỷ bản lưu.

### Rủi ro cần kiểm tra

- **Bảng mã nhiều byte** (`vniWindows`, `cp1258` — `singleBackspacePerLetter == false`): một ⌫ chỉ xoá một byte. Vẫn an toàn vì ⌫ sau dấu cách xoá đúng dấu cách (1 byte); nhưng cần test kỹ phần `diff` khi áp dấu lên từ đã nạp lại.
- **Trình duyệt có autocomplete** (`browserAutocompleteFix`) và **Electron** (`electronDelayMicros`): thanh địa chỉ có thể tự điền sau dấu cách. Chạy `tools/stress/run_all.sh` trên Chrome/Safari/VS Code/Slack.
- **Ứng dụng tự sửa chính tả** (TextEdit, Notes) có thể đổi từ sau dấu cách. Nếu nội dung màn hình khác `onScreen`, kết quả sẽ sai; nên kiểm tra bằng `--live-test` trong TextEdit.

## 4. Cài đặt (UI)

`Settings` (`App/Settings/Preferences.swift`) thêm 2 trường, đều có giá trị mặc định khi giải mã để tương thích file cài đặt cũ:

```swift
var escRestore = true
var editPreviousWord = true
```

- Nối vào `engineConfig(...)`.
- `SettingsView`, mục cài đặt gõ (cạnh “Tự khôi phục từ tiếng Anh”):
  - `KToggleRow(title: tr("Nhấn ESC để khôi phục phím đã gõ", "Press ESC to restore typed keys"), …)`
  - `KToggleRow(title: tr("Xoá dấu cách để sửa dấu từ trước", "Backspace over space to edit previous word"), …)`
- Cập nhật `KVietKey-Huong-dan-su-dung.docx` và README (mục tính năng).

## 5. Kiểm thử

### Unit test (`Packages/VietEngine/Tests/VietEngineTests`)

`Typist` cần thêm ký hiệu cho ESC (ví dụ `"\u{1B}"`) gọi `engine.restoreRaw()` và áp kết quả; khi trả về `nil` thì không làm gì với màn hình.

| Input | Kết quả |
|---|---|
| `test⎋` | `test` |
| `test⎋s ` | `tests ` |
| `bas⎋` | `bas` |
| `dd⎋` | `dd` |
| `viet⎋` (chưa biến đổi) | `viet` (ESC đi qua) |
| `⎋` (không có từ) | `` |
| `test⎋` khi `escRestore = false` | `tét` |
| `viet ⌫j` | `việt` |
| `tieng ⌫s ` | `tiếng ` |
| `viet ⌫⌫j` | ⌫ thứ hai xoá `t` theo logic ⌫ trong từ, rồi `j` áp vào `vie` |
| `viet x⌫⌫j` | `vietj` (đã gõ phím khác nên không nạp lại) |
| `viet ⌫j` khi `editPreviousWord = false` | `vietj` |
| `vn ⌫` với gõ tắt `vn → Việt Nam` | không nạp lại (từ đã bị thay thế) |
| `Vieet. ⌫` với `autoCapitalize` | `capState` khôi phục đúng |

Chạy thêm với `charset = .vniWindows` và `.tcvn3` cho các ca chính.

### Kiểm thử thực tế

- `KVietKey --selftest`
- `tools/stress/run_textedit.sh` và `tools/stress/run_all.sh`
- Thủ công: TextEdit, Notes, Safari/Chrome (ô thường và thanh địa chỉ), VS Code, Slack, Terminal; hộp thoại có nút Cancel gắn ESC (ESC phải vẫn đóng hộp thoại khi không có từ bị biến đổi).

## 6. Ngoài phạm vi

- Sửa nhiều từ trước (ngăn xếp nhiều mức).
- Sửa từ sau Enter/Tab/dấu câu.
- Đọc nội dung thật trên màn hình qua Accessibility API để đồng bộ bộ đệm.
