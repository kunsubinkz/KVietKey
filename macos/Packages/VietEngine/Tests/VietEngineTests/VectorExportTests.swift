import XCTest
@testable import VietEngine

/// Xuất vector cho các phần ngoài `Typist`: bảng mã, gõ tắt, kiểm tra chính tả. Không làm gì nếu không đặt `KVK_EXPORT_VECTORS`.
final class VectorExportTests: XCTestCase {
    func testExportCharsets() {
        guard VectorRecorder.directory != nil else { return }
        var samples = CharsetTests.all.map { String($0) }
        samples += [
            "Tiếng Việt rất đẹp, người Hà Nội nói: được rồi!", "tiếng việt", "ĐẮK LẮK", "Nguyễn Thị Hương",
            "plain ascii 123", "hoà thuý khoẻ", "ươ uơ ưu", "",
        ]
        samples += CorpusTests.words.prefix(300)
        var rows: [[String: Any]] = []
        for cs in Charset.allCases {
            for text in samples {
                let encoded = cs.encode(text)
                rows.append(["charset": cs.rawValue, "text": text, "encoded": encoded, "decoded": cs.decode(encoded)])
            }
        }
        VectorRecorder.writeExtra(name: "charset", ["rows": rows])
    }

    func testExportMacros() {
        guard VectorRecorder.directory != nil else { return }
        let entries = ["vn": "Việt Nam", "dc": "được", "hcm": "Hồ Chí Minh", "Ab": "exact"]
        let table = MacroTable(entries: entries)
        var expand: [[String: Any]] = []
        for typed in ["vn", "VN", "Vn", "vN", "dc", "Dc", "DC", "hcm", "HCM", "Hcm", "Ab", "ab", "AB", "x", "", "v", "V"] {
            for keepCase in [true, false] {
                var row: [String: Any] = ["typed": typed, "keepCase": keepCase]
                if let r = table.expand(typed, keepCase: keepCase) { row["expected"] = r }
                expand.append(row)
            }
        }
        var parse: [[String: Any]] = []
        for text in ["vn: Việt Nam\n\nbad line\ndc:được\n", "a:b:c\n", ":x\ny:\n", "  k  :  v v  \r\nz:1\r\n", ""] {
            let parsed = MacroTable.parse(text: text)
            parse.append(["text": text, "entries": parsed.entries, "export": parsed.exportText()])
        }
        VectorRecorder.writeExtra(name: "macro", ["entries": entries, "expand": expand, "parse": parse])
    }

    /// Dựng bộ đệm engine từ một từ tiếng Việt: chữ gốc + dấu phụ (mũ, trăng, móc, gạch đ); dấu thanh bị tách ra.
    private func vchars(_ word: String) -> [VChar] {
        var out: [VChar] = []
        for ch in word {
            if ch.lowercased() == "đ" { out.append(VChar(base: "d", mark: .stroke, upper: ch.isUppercase)); continue }
            var v: VChar?
            for u in String(ch).decomposedStringWithCanonicalMapping.unicodeScalars {
                switch u {
                case "\u{0302}": v?.mark = .circumflex
                case "\u{0306}": v?.mark = .breve
                case "\u{031B}": v?.mark = .horn
                case "\u{0301}", "\u{0300}", "\u{0309}", "\u{0303}", "\u{0323}": break
                default: v = VChar(base: Character(String(u).lowercased()), upper: String(u) != String(u).lowercased())
                }
            }
            if let v { out.append(v) }
        }
        return out
    }

    func testExportSpellChecker() {
        guard VectorRecorder.directory != nil else { return }
        var rows: [[String: Any]] = []
        let words = Array(Set(CorpusTests.words)).sorted()
            + ["class", "show", "windows", "za", "fi", "ka", "ke", "ngi", "ghi", "gha", "cia", "kia", "qu", "xyz", "đăk", "nông"]
        for word in words {
            let chars = vchars(word)
            for foreign in [false, true] {
                for tone in Tone.allCases {
                    rows.append(["word": word, "tone": tone.rawValue, "foreign": foreign,
                                 "valid": SpellChecker.isValid(chars, tone: tone, allowForeign: foreign),
                                 "initialValid": SpellChecker.initialIsValid(chars, allowForeign: foreign)])
                }
            }
        }
        VectorRecorder.writeExtra(name: "spell", ["rows": rows])
    }
}
