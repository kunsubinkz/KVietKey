import Foundation

public struct EngineConfig: Equatable, Sendable {
    public var method: InputMethod = .telex
    public var charset: Charset = .unicode
    /// Đặt dấu kiểu mới (hoà, thuý) thay vì kiểu cũ (hòa, thúy).
    public var modernTone = true
    /// Cho phép bỏ dấu/áp dấu phụ ở bất kỳ vị trí nào trong từ (vd. `tieengs`, `tungw`).
    public var freeMarking = true
    /// Không biến đổi khi phụ âm đầu không hợp lệ (vd. `class`, `show`).
    public var spellCheck = true
    /// Khi ngắt từ, nếu từ không hợp lệ thì khôi phục đúng các phím đã gõ (vd. `windows`).
    public var autoRestore = true
    public var autoCapitalize = false
    /// Cho phép z, f, w, j làm phụ âm đầu.
    public var allowZFWJ = false
    /// Phím `w` đứng một mình thành `ư` (Telex).
    public var wAsU = true
    public var macroEnabled = true
    public var macroKeepCase = true

    public init() {}
}

private enum CapState { case none, afterPunct, armed }

/// Bộ máy gõ tiếng Việt: nhận từng ký tự, trả về thao tác xóa/gõ cần thực hiện.
public final class Engine {
    public var config: EngineConfig
    public var macros = MacroTable()

    private var chars: [VChar] = []
    private var tone: Tone = .none
    private var raw: [Character] = []
    private var undone = Set<Character>()
    private var rawReliable = true
    /// Văn bản (đã mã hóa theo bảng mã đích) của từ hiện tại đang có trên màn hình.
    private var onScreen = ""
    private var capState: CapState = .none

    public init(config: EngineConfig = EngineConfig()) {
        self.config = config
    }

    // MARK: Trạng thái

    /// Từ đang gõ (Unicode), phục vụ debug/test.
    public var currentWord: String { renderUnicode() }
    public var hasPendingWord: Bool { !chars.isEmpty }

    /// Xóa bộ đệm: dùng khi click chuột, đổi ứng dụng, phím mũi tên, ⌘/⌃ + phím…
    public func reset() {
        chars.removeAll()
        tone = .none
        raw.removeAll()
        undone.removeAll()
        rawReliable = true
        onScreen = ""
        capState = .none
    }

    private func resetWord() {
        chars.removeAll()
        tone = .none
        raw.removeAll()
        undone.removeAll()
        rawReliable = true
        onScreen = ""
    }

    // MARK: Xử lý phím

    /// Xử lý một ký tự người dùng gõ (đã tính Shift/CapsLock).
    public func process(_ input: Character) -> EngineResult {
        var key = input
        let isASCIILetter = input.isASCII && input.isLetter

        if isASCIILetter, chars.isEmpty, capState == .armed {
            key = Character(input.uppercased())
            capState = .none
        } else if isASCIILetter, capState != .none {
            capState = .none
        }

        let lower = Character(key.lowercased())
        if let action = config.method.action(for: lower), !undone.contains(lower) {
            let backup = (chars, tone, undone)
            if apply(action, key: key) {
                raw.append(key)
                return refresh(typed: input)
            }
            (chars, tone, undone) = backup
        }

        guard isASCIILetter else { return processBreak(input) }
        appendLiteral(key)
        raw.append(key)
        return refresh(typed: input)
    }

    /// Xử lý phím ngắt từ (dấu cách, Enter, Tab, dấu câu…). Phần thay thế (nếu có) phải được gõ
    /// trước khi chuyển tiếp phím gốc.
    public func processBreak(_ input: Character) -> EngineResult {
        var result = EngineResult.pass
        if !chars.isEmpty {
            if config.macroEnabled, let expansion = macroExpansion() {
                result = replace(with: expansion)
            } else if config.autoRestore, rawReliable, undone.isEmpty, shouldRestore() {
                result = replace(with: String(raw))
            }
        }
        resetWord()
        updateCapState(afterBreak: input)
        return result
    }

    /// Người dùng nhấn Backspace (phím vẫn được chuyển tiếp tới ứng dụng).
    public func backspace() {
        guard !chars.isEmpty else { return }
        if !config.charset.singleBackspacePerLetter {
            resetWord()
            return
        }
        chars = displayChars()
        chars.removeLast()
        if !chars.contains(where: { $0.isVowel }) { tone = .none }
        raw = chars.map { $0.base }
        rawReliable = false
        onScreen = config.charset.encode(renderUnicode())
        if chars.isEmpty { resetWord() }
    }

    // MARK: Áp dụng hành động

    private func apply(_ action: KeyAction, key: Character) -> Bool {
        switch action {
        case .tone(let t): return applyTone(t, key: key)
        case .clearTone:
            guard tone != .none else { return false }
            tone = .none
            return true
        case .doubleVowel(let v): return applyDoubleVowel(v, key: key)
        case .doubleD: return applyDoubleD(key: key)
        case .w: return applyHornBreve(.w, key: key)
        case .circumflex: return applyCircumflex(key: key)
        case .horn: return applyHornBreve(.horn, key: key)
        case .breve: return applyHornBreve(.breve, key: key)
        case .stroke: return applyStroke(key: key)
        }
    }

    private func appendLiteral(_ key: Character) {
        let s = String(key)
        chars.append(VChar(base: Character(s.lowercased()), upper: key.isUppercase))
    }

    private func spellGate() -> Bool {
        !config.spellCheck || SpellChecker.initialIsValid(chars, allowForeign: config.allowZFWJ)
    }

    private func undo(_ key: Character) -> Bool {
        appendLiteral(key)
        undone.insert(Character(String(key).lowercased()))
        return true
    }

    private func applyTone(_ t: Tone, key: Character) -> Bool {
        guard chars.contains(where: { $0.isVowel }), spellGate() else { return false }
        if !config.freeMarking, let last = chars.last, !last.isVowel { return false }
        if tone == t {
            // Chỉ hoàn tác khi gõ lặp phím dấu ngay liền sau (ss, ff…). Lặp cách xa (response, order) là chữ thường,
            // để nguyên ký tự rồi khôi phục khi ngắt từ.
            guard let previous = raw.last, Character(previous.lowercased()) == Character(String(key).lowercased()) else { return false }
            tone = .none
            return undo(key)
        }
        tone = t
        return true
    }

    private func applyDoubleVowel(_ v: Character, key: Character) -> Bool {
        guard let last = chars.last, spellGate() else { return false }
        guard last.base == v else {
            // Gõ dấu mũ sau phụ âm cuối / nguyên âm khác: nhapa → nhâp, cuoio → cuôi, tiene → tiên.
            guard config.freeMarking, let i = nucleusIndex(of: v), chars[i].mark == .none else { return false }
            chars[i].mark = .circumflex
            // Chỉ khi ra vần hợp lệ: ngoeo (ngoèo) không thành "ngôe".
            guard SpellChecker.isValid(chars, tone: .none, allowForeign: config.allowZFWJ) else {
                chars[i].mark = .none
                return false
            }
            return true
        }
        switch last.mark {
        case .circumflex:
            chars[chars.count - 1].mark = .none
            return undo(key)
        case .none:
            chars[chars.count - 1].mark = .circumflex
            return true
        default:
            return false
        }
    }

    private func applyDoubleD(key: Character) -> Bool {
        guard let last = chars.last, spellGate() else { return false }
        guard last.base == "d" else {
            // Gõ d thứ hai sau nguyên âm: dud → đu, dudowcj → được.
            guard config.freeMarking, chars.count >= 2, chars[0].base == "d", chars[0].mark == .none,
                  nucleusRange() != nil else { return false }
            chars[0].mark = .stroke
            return true
        }
        if last.mark == .stroke {
            chars[chars.count - 1].mark = .none
            return undo(key)
        }
        guard last.mark == .none else { return false }
        chars[chars.count - 1].mark = .stroke
        return true
    }

    private func applyStroke(key: Character) -> Bool {
        guard spellGate() else { return false }
        let idx: Int?
        if config.freeMarking {
            idx = chars.first?.base == "d" ? 0 : nil
        } else {
            idx = chars.last?.base == "d" ? chars.count - 1 : nil
        }
        guard let i = idx else { return false }
        if chars[i].mark == .stroke {
            chars[i].mark = .none
            return undo(key)
        }
        chars[i].mark = .stroke
        return true
    }

    private func applyCircumflex(key: Character) -> Bool {
        guard spellGate(), let i = targetIndex(in: ["a", "e", "o"]) else { return false }
        if chars[i].mark == .circumflex {
            chars[i].mark = .none
            return undo(key)
        }
        chars[i].mark = .circumflex
        return true
    }

    private enum HornKind { case w, horn, breve }

    private func applyHornBreve(_ kind: HornKind, key: Character) -> Bool {
        guard spellGate() else { return false }

        if kind == .w, let last = chars.last, last.fromW {
            chars.removeLast()
            return undo(key)
        }

        if kind != .breve, let i = uoPairIndex() {
            let u = chars[i], o = chars[i + 1]
            if u.mark == .horn, o.mark == .horn {
                chars[i].mark = .none
                chars[i + 1].mark = .none
                return undo(key)
            }
            if o.mark != .circumflex {
                chars[i].mark = .horn
                chars[i + 1].mark = .horn
                return true
            }
        }

        // Cụm "ua" (không sau q): w móc vào u → mưa, nữa, cửa (không phải "uă").
        if kind != .breve, let r = nucleusRange(), r.count == 2,
           chars[r.lowerBound].base == "u", chars[r.lowerBound + 1].base == "a",
           chars[r.lowerBound + 1].mark == .none,
           !(r.lowerBound > 0 && chars[r.lowerBound - 1].base == "q") {
            let i = r.lowerBound
            if chars[i].mark == .horn {
                chars[i].mark = .none
                return undo(key)
            }
            chars[i].mark = .horn
            return true
        }

        let allowed: Set<Character>
        switch kind {
        case .w: allowed = ["a", "o", "u"]
        case .horn: allowed = ["o", "u"]
        case .breve: allowed = ["a"]
        }
        if let i = targetIndex(in: allowed) {
            let want: Mark = chars[i].base == "a" ? .breve : .horn
            if chars[i].mark == want {
                chars[i].mark = .none
                return undo(key)
            }
            chars[i].mark = want
            return true
        }

        if kind == .w, config.wAsU, !chars.contains(where: { $0.isVowel }) {
            chars.append(VChar(base: "u", mark: .horn, upper: key.isUppercase, fromW: true))
            return true
        }
        return false
    }

    /// Cụm nguyên âm duy nhất của từ, chỉ khi phần sau nó là phụ âm cuối hợp lệ (hoặc trống).
    /// Từ có hai cụm nguyên âm (banana, data…) không phải âm tiết tiếng Việt → nil.
    private func nucleusRange() -> Range<Int>? {
        guard let start = chars.firstIndex(where: { $0.isVowel }) else { return nil }
        var end = start
        while end < chars.count, chars[end].isVowel { end += 1 }
        // "gi", "qu": nguyên âm đầu thuộc phụ âm đầu nhưng vẫn nằm trong cụm, không ảnh hưởng.
        let tail = String(chars[end...].map { $0.base })
        guard ["", "c", "ch", "m", "n", "ng", "nh", "p", "t"].contains(tail) else { return nil }
        return start..<end
    }

    /// Vị trí nguyên âm `v` (gần cuối nhất) trong cụm nguyên âm của từ.
    private func nucleusIndex(of v: Character) -> Int? {
        guard let r = nucleusRange() else { return nil }
        return r.reversed().first { chars[$0].base == v }
    }

    /// Chỉ số của `u` trong cặp `uo` (không tính `qu`) để gõ `ươ`.
    private func uoPairIndex() -> Int? {
        guard chars.count >= 2 else { return nil }
        for i in 0..<(chars.count - 1) where chars[i].base == "u" && chars[i + 1].base == "o" {
            if i > 0, chars[i - 1].base == "q" { continue }
            return i
        }
        return nil
    }

    /// Ký tự gần cuối nhất thuộc `allowed` (u sau q bị bỏ qua). Khi tắt bỏ dấu tự do phải là ký tự cuối.
    private func targetIndex(in allowed: Set<Character>) -> Int? {
        for i in stride(from: chars.count - 1, through: 0, by: -1) {
            let c = chars[i]
            guard allowed.contains(c.base) else { continue }
            if c.base == "u", i > 0, chars[i - 1].base == "q" { continue }
            if !config.freeMarking, i != chars.count - 1 { return nil }
            return i
        }
        return nil
    }

    // MARK: Dựng kết quả

    /// `ươ` ở cuối từ (chưa có phụ âm cuối) hiển thị là `uơ` như trong "thuở", "huơ";
    /// khi gõ thêm phụ âm cuối hoặc nguyên âm thì `u` lại có móc ("thương", "người").
    private func displayChars() -> [VChar] {
        var out = chars
        let n = out.count
        if n >= 2, out[n - 1].base == "o", out[n - 1].mark == .horn,
           out[n - 2].base == "u", out[n - 2].mark == .horn,
           !(n >= 3 && out[n - 3].base == "q") {
            out[n - 2].mark = .none
        }
        return out
    }

    private func renderUnicode() -> String {
        let display = displayChars()
        let position = ToneMarker.position(display, modern: config.modernTone)
        var out = ""
        for (i, c) in display.enumerated() {
            out += c.unicode(tone: i == position ? tone : .none)
        }
        return out
    }

    private func refresh(typed: Character) -> EngineResult {
        let new = config.charset.encode(renderUnicode())
        let result = diff(from: onScreen, to: new)
        onScreen = new
        if result.backspaces == 0, result.text == String(typed) {
            return .pass
        }
        return EngineResult(backspaces: result.backspaces, text: result.text, passthrough: false)
    }

    private func diff(from old: String, to new: String) -> (backspaces: Int, text: String) {
        let a = Array(old), b = Array(new)
        var common = 0
        while common < a.count, common < b.count, a[common] == b[common] { common += 1 }
        return (a.count - common, String(b[common...]))
    }

    private func replace(with text: String) -> EngineResult {
        let encoded = config.charset.encode(text)
        let result = diff(from: onScreen, to: encoded)
        if result.backspaces == 0, result.text.isEmpty { return .pass }
        return EngineResult(backspaces: result.backspaces, text: result.text, passthrough: true)
    }

    private func macroExpansion() -> String? {
        guard !macros.isEmpty else { return nil }
        return macros.expand(String(raw), keepCase: config.macroKeepCase)
    }

    private func shouldRestore() -> Bool {
        if String(raw) == renderUnicode() { return false }
        return !SpellChecker.isValid(displayChars(), tone: tone, allowForeign: config.allowZFWJ)
    }

    private func updateCapState(afterBreak ch: Character) {
        guard config.autoCapitalize else { capState = .none; return }
        switch ch {
        case ".", "!", "?": capState = .afterPunct
        case " ": capState = capState == .afterPunct ? .armed : (capState == .armed ? .armed : .none)
        case "\n", "\r": capState = .armed
        case "\t": break
        default: capState = .none
        }
    }
}
