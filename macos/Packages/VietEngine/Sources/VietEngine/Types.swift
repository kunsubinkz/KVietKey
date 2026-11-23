import Foundation

/// Dấu thanh: sắc, huyền, hỏi, ngã, nặng.
public enum Tone: Int, CaseIterable, Sendable {
    case none, acute, grave, hook, tilde, dot

    var combining: Unicode.Scalar? {
        switch self {
        case .none: return nil
        case .acute: return "\u{0301}"
        case .grave: return "\u{0300}"
        case .hook: return "\u{0309}"
        case .tilde: return "\u{0303}"
        case .dot: return "\u{0323}"
        }
    }
}

/// Dấu phụ của chữ cái: mũ (â ê ô), trăng (ă), móc (ơ ư), gạch (đ).
enum Mark {
    case none, circumflex, breve, horn, stroke

    var combining: Unicode.Scalar? {
        switch self {
        case .circumflex: return "\u{0302}"
        case .breve: return "\u{0306}"
        case .horn: return "\u{031B}"
        case .none, .stroke: return nil
        }
    }
}

/// Một ký tự trong bộ đệm từ hiện tại. `base` luôn là chữ thường.
struct VChar: Equatable {
    var base: Character
    var mark: Mark = .none
    var upper = false
    /// Ký tự `ư` sinh ra từ phím `w` đứng một mình (để gõ `ww` hoàn tác về `w`).
    var fromW = false

    var isVowel: Bool { "aeiouy".contains(base) }

    /// Nhãn chữ thường không dấu thanh, dùng cho kiểm tra chính tả (vd. "ê", "đ").
    var label: Character {
        switch (base, mark) {
        case ("a", .circumflex): return "â"
        case ("a", .breve): return "ă"
        case ("e", .circumflex): return "ê"
        case ("o", .circumflex): return "ô"
        case ("o", .horn): return "ơ"
        case ("u", .horn): return "ư"
        case ("d", .stroke): return "đ"
        default: return base
        }
    }

    func unicode(tone: Tone) -> String {
        if base == "d", mark == .stroke { return upper ? "Đ" : "đ" }
        var scalars = String.UnicodeScalarView()
        scalars.append(contentsOf: String(base).unicodeScalars)
        if let m = mark.combining { scalars.append(m) }
        if let t = tone.combining { scalars.append(t) }
        let composed = String(scalars).precomposedStringWithCanonicalMapping
        return upper ? composed.uppercased() : composed
    }
}

/// Kết quả xử lý một phím.
public struct EngineResult: Equatable, Sendable {
    /// Số ký tự cần xóa trên màn hình trước khi gõ `text`.
    public var backspaces: Int
    /// Văn bản cần gõ ra sau khi xóa.
    public var text: String
    /// `true` nếu phím gốc vẫn phải được chuyển tiếp tới ứng dụng (sau phần thay thế, nếu có).
    public var passthrough: Bool

    public var isEmpty: Bool { backspaces == 0 && text.isEmpty }

    public static let pass = EngineResult(backspaces: 0, text: "", passthrough: true)
}
