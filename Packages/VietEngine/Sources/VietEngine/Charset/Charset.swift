import Foundation

/// Bảng mã đầu ra. Engine luôn dựng từ bằng Unicode dựng sẵn (NFC) rồi chuyển sang bảng mã đích.
///
/// Với các bảng mã cũ (TCVN3, VNI Windows, VISCII, CP1258) mỗi byte được xuất ra thành ký tự
/// có điểm mã bằng giá trị byte — dùng với phông chữ tương ứng, giống các bộ gõ khác.
public enum Charset: String, CaseIterable, Codable, Sendable {
    case unicode, unicodeComposite, tcvn3, vniWindows, viscii, cp1258

    public var displayName: String {
        switch self {
        case .unicode: return "Unicode dựng sẵn"
        case .unicodeComposite: return "Unicode tổ hợp"
        case .tcvn3: return "TCVN3 (ABC)"
        case .vniWindows: return "VNI Windows"
        case .viscii: return "VISCII"
        case .cp1258: return "CP1258"
        }
    }

    /// Mỗi ký tự tiếng Việt chiếm đúng một lần Backspace trên màn hình.
    var singleBackspacePerLetter: Bool {
        switch self {
        case .unicode, .unicodeComposite, .tcvn3, .viscii: return true
        case .vniWindows, .cp1258: return false
        }
    }

    private var table: [(UInt16, [UInt8])]? {
        switch self {
        case .tcvn3: return CharsetTables.tcvn3
        case .vniWindows: return CharsetTables.vniWindows
        case .viscii: return CharsetTables.viscii
        case .cp1258: return CharsetTables.cp1258
        case .unicode, .unicodeComposite: return nil
        }
    }

    private struct Maps {
        var encode: [Character: String] = [:]
        var decode: [String: Character] = [:]
        var maxKeyLength = 1
    }

    private static let cache: [Charset: Maps] = {
        var result: [Charset: Maps] = [:]
        for cs in Charset.allCases {
            guard let table = cs.table else { continue }
            var maps = Maps()
            for (code, bytes) in table {
                guard let scalar = Unicode.Scalar(code) else { continue }
                let ch = Character(scalar)
                let legacy = String(String.UnicodeScalarView(bytes.map { Unicode.Scalar($0) }))
                maps.encode[ch] = legacy
                // Bảng mã chữ hoa/thường dùng chung mã: ưu tiên chữ thường khi giải mã.
                if let existing = maps.decode[legacy], !existing.isLowercase { maps.decode[legacy] = ch }
                else if maps.decode[legacy] == nil { maps.decode[legacy] = ch }
                maps.maxKeyLength = max(maps.maxKeyLength, bytes.count)
            }
            result[cs] = maps
        }
        return result
    }()

    /// Chuyển văn bản Unicode dựng sẵn sang bảng mã này.
    public func encode(_ text: String) -> String {
        switch self {
        case .unicode:
            return text
        case .unicodeComposite:
            return text.decomposedStringWithCanonicalMapping
        default:
            guard let map = Charset.cache[self]?.encode else { return text }
            var out = ""
            out.reserveCapacity(text.utf8.count)
            for ch in text.precomposedStringWithCanonicalMapping {
                if let legacy = map[ch] { out += legacy } else { out.append(ch) }
            }
            return out
        }
    }

    /// Chuyển văn bản đang ở bảng mã này về Unicode dựng sẵn.
    public func decode(_ text: String) -> String {
        switch self {
        case .unicode, .unicodeComposite:
            return text.precomposedStringWithCanonicalMapping
        default:
            guard let maps = Charset.cache[self] else { return text }
            let scalars = Array(text.unicodeScalars)
            var out = ""
            var i = 0
            while i < scalars.count {
                var matched = false
                var len = min(maps.maxKeyLength, scalars.count - i)
                while len >= 1 {
                    var key = String.UnicodeScalarView()
                    key.append(contentsOf: scalars[i..<(i + len)])
                    if let ch = maps.decode[String(key)] {
                        out.append(ch)
                        i += len
                        matched = true
                        break
                    }
                    len -= 1
                }
                if !matched {
                    out.unicodeScalars.append(scalars[i])
                    i += 1
                }
            }
            return out
        }
    }
}
