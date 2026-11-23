import Foundation

/// Hành động mà một phím gõ có thể kích hoạt.
enum KeyAction: Equatable {
    case tone(Tone)
    case clearTone
    /// Gõ đôi nguyên âm (Telex): aa, ee, oo → â, ê, ô.
    case doubleVowel(Character)
    /// Gõ đôi d (Telex, VIQR): dd → đ.
    case doubleD
    /// Phím `w` của Telex: ă ơ ư.
    case w
    case circumflex
    case horn
    case breve
    case stroke
}

public enum InputMethod: String, CaseIterable, Codable, Sendable {
    case telex, vni, viqr, combined

    public var displayName: String {
        switch self {
        case .telex: return "Telex"
        case .vni: return "VNI"
        case .viqr: return "VIQR"
        case .combined: return "Telex + VNI"
        }
    }

    func action(for key: Character) -> KeyAction? {
        switch self {
        case .telex: return InputMethod.telexAction(key)
        case .vni: return InputMethod.vniAction(key)
        case .viqr: return InputMethod.viqrAction(key)
        case .combined: return InputMethod.telexAction(key) ?? InputMethod.vniAction(key)
        }
    }

    private static func telexAction(_ key: Character) -> KeyAction? {
        switch key {
        case "s": return .tone(.acute)
        case "f": return .tone(.grave)
        case "r": return .tone(.hook)
        case "x": return .tone(.tilde)
        case "j": return .tone(.dot)
        case "z": return .clearTone
        case "a", "e", "o": return .doubleVowel(key)
        case "d": return .doubleD
        case "w": return .w
        default: return nil
        }
    }

    private static func vniAction(_ key: Character) -> KeyAction? {
        switch key {
        case "1": return .tone(.acute)
        case "2": return .tone(.grave)
        case "3": return .tone(.hook)
        case "4": return .tone(.tilde)
        case "5": return .tone(.dot)
        case "0": return .clearTone
        case "6": return .circumflex
        case "7": return .horn
        case "8": return .breve
        case "9": return .stroke
        default: return nil
        }
    }

    private static func viqrAction(_ key: Character) -> KeyAction? {
        switch key {
        case "'": return .tone(.acute)
        case "`": return .tone(.grave)
        case "?": return .tone(.hook)
        case "~": return .tone(.tilde)
        case ".": return .tone(.dot)
        case "-": return .clearTone
        case "^": return .circumflex
        case "(": return .breve
        case "+": return .horn
        case "d": return .doubleD
        default: return nil
        }
    }
}
