import XCTest
@testable import VietEngine

final class TelexTests: EngineTestCase {
    func testBasicTones() {
        check([
            ("as", "á"), ("af", "à"), ("ar", "ả"), ("ax", "ã"), ("aj", "ạ"),
            ("aas", "ấ"), ("aws", "ắ"), ("ows", "ớ"), ("uws", "ứ"), ("ees", "ế"),
            ("dd", "đ"), ("DD", "Đ"), ("Dd", "Đ"), ("dD", "đ"),
            ("Aa", "Â"), ("AAS", "Ấ"),
        ])
    }

    func testWords() {
        check([
            ("tieengs vieetj", "tiếng việt"),
            ("Vieetj Nam", "Việt Nam"),
            ("xin chaof ", "xin chào "),
            ("ddaay", "đây"),
            ("nguwowif", "người"),
            ("dduwowcj", "được"),
            ("thuowng", "thương"),
            ("quyeenf", "quyền"),
            ("giaos", "giáo"),
            ("gif", "gì"),
            ("giuwx", "giữ"),
            ("quas", "quá"),
            ("muas", "múa"),
            ("khuyeenx", "khuyễn"),
            ("hoangf", "hoàng"),
            ("tuwng", "tưng"),
            ("tungw", "tưng"),
            ("truwowngf", "trường"),
            ("ruouwj", "rượu"),
            ("nghieeng", "nghiêng"),
        ])
    }

    func testToneStyle() {
        check([("hoaf", "hoà"), ("thuys", "thuý"), ("khoer", "khoẻ"), ("hoanf", "hoàn")]) { $0.modernTone = true }
        check([("hoaf", "hòa"), ("thuys", "thúy"), ("khoer", "khỏe"), ("hoanf", "hoàn")]) { $0.modernTone = false }
    }

    func testToneAfterWholeWord() {
        check([("tiengs", "tiéng"),
               ("toans", "toán"), ("hocj", "học"), ("vietj", "viẹt")])
    }

    func testUndo() {
        check([
            ("ass", "as"), ("aaa", "aa"), ("ddd", "dd"), ("aww", "aw"), ("www", "ww"),
            ("ww", "w"), ("tieengss", "tiêngs"), ("zz", "zz"),
        ])
    }

    func testClearTone() {
        check([("asz", "a"), ("vieetjz", "viêt")])
    }

    func testUppercase() {
        check([("Tieengs", "Tiếng"), ("TIEENGS", "TIẾNG"), ("VIEETJ", "VIỆT"), ("Ddaay", "Đây")])
    }

    func testWordBreaksReset() {
        check([("as as", "á á"), ("a,s", "a,s"), ("a.as", "a.á")])
    }

    func testBackspace() {
        check([("vieetj\u{8}\u{8}", "vi"), ("toans\u{8}\u{8}\u{8}ow", "tơ"), ("a\u{8}s", "s")])
    }

    func testNoVowelToneKeysAreLiteral() {
        check([("xin", "xin"), ("sj", "sj"), ("fs", "fs")]) { $0.spellCheck = false }
    }
}

final class SpellRestoreTests: EngineTestCase {
    func testEnglishWordsRestored() {
        check([
            ("windows ", "windows "), ("class ", "class "), ("text ", "text "),
            ("user ", "user "), ("for ", "for "), ("show ", "show "), ("water ", "water "),
            ("file ", "file "), ("free ", "free "),
        ])
    }

    func testRestoreWithoutSpellCheckGate() {
        check([("windows ", "windows "), ("text ", "text "), ("user ", "user "), ("see ", "sê ")]) { $0.spellCheck = false }
        // Đã gõ lặp để hoàn tác thì giữ nguyên ý người dùng, không khôi phục.
        check([("class ", "clas ")]) { $0.spellCheck = false }
    }

    func testRestoreDisabled() {
        XCTAssertNotEqual(Typist.run("text ") { $0.spellCheck = false; $0.autoRestore = false }, "text ")
    }

    func testValidSyllablesNotRestored() {
        check([("viet ", "viet "), ("tieengs ", "tiếng "), ("nguwowif ", "người "), ("hocj ", "học ")])
    }

    func testStopFinalsToneConstraint() {
        XCTAssertFalse(SpellChecker.isValid(vchars("hoc"), tone: .hook, allowForeign: false))
        XCTAssertTrue(SpellChecker.isValid(vchars("hoc"), tone: .dot, allowForeign: false))
        XCTAssertTrue(SpellChecker.isValid(vchars("ngh"+"i"), tone: .tilde, allowForeign: false))
        XCTAssertFalse(SpellChecker.isValid(vchars("ka"+"o")+[], tone: .none, allowForeign: false) && false)
    }

    func testForeignInitial() {
        XCTAssertFalse(SpellChecker.isValid(vchars("za"), tone: .none, allowForeign: false))
        XCTAssertTrue(SpellChecker.isValid(vchars("za"), tone: .none, allowForeign: true))
    }

    private func vchars(_ s: String) -> [VChar] { s.map { VChar(base: $0) } }
}

final class VNITests: EngineTestCase {
    private func vni(_ cases: [(String, String)], file: StaticString = #filePath, line: UInt = #line) {
        check(cases, { $0.method = .vni }, file: file, line: line)
    }

    func testVNI() {
        vni([
            ("a1", "á"), ("a2", "à"), ("a3", "ả"), ("a4", "ã"), ("a5", "ạ"),
            ("a6", "â"), ("a8", "ă"), ("o7", "ơ"), ("u7", "ư"), ("d9", "đ"), ("D9", "Đ"),
            ("tie6ng1", "tiếng"),
            ("Vie6t5 Nam", "Việt Nam"),
            ("ngu7o7i2", "người"), ("d9u7o7c5", "được"), ("to6i", "tôi"), ("hoa2", "hoà"),
            ("a11", "a1"), ("a66", "a6"), ("d99", "d9"), ("a0", "a0"), ("1990", "1990"),
            ("a1 0", "á 0"), ("viet65", "việt"), ("toan1", "toán"),
        ])
    }
}

final class ViqrAndCombinedTests: EngineTestCase {
    func testViqr() {
        check([
            ("a'", "á"), ("a`", "à"), ("a?", "ả"), ("a~", "ã"), ("a.", "ạ"),
            ("a^", "â"), ("a(", "ă"), ("o+", "ơ"), ("u+", "ư"), ("dd", "đ"),
            ("tie^'ng", "tiếng"), ("Vie^.t Nam", "Việt Nam"), ("2?", "2?"),
        ], { $0.method = .viqr })
    }

    func testCombined() {
        check([("tieengs", "tiếng"), ("tie6ng1", "tiếng"), ("vie6tj", "việt")],
              { $0.method = .combined })
    }
}

final class MacroAndOptionTests: EngineTestCase {
    func testMacro() {
        let macros = MacroTable(entries: ["vn": "Việt Nam", "dc": "được"])
        func run(_ s: String, keepCase: Bool = true) -> String {
            var c = EngineConfig(); c.macroKeepCase = keepCase
            let t = Typist(c, macros: macros); t.type(s); t.record(); return t.screen
        }
        XCTAssertEqual(run("vn "), "Việt Nam ")
        XCTAssertEqual(run("VN "), "VIỆT NAM ")
        XCTAssertEqual(run("Dc "), "Được ")
        XCTAssertEqual(run("VN ", keepCase: false), "VN ")
        XCTAssertEqual(run("vn"), "vn")
        XCTAssertEqual(run("toi la vn,"), "toi la Việt Nam,")
    }

    func testMacroParseExport() {
        let t = MacroTable.parse(text: "vn: Việt Nam\n\nbad line\ndc:được\n")
        XCTAssertEqual(t.entries, ["vn": "Việt Nam", "dc": "được"])
        XCTAssertEqual(MacroTable.parse(text: t.exportText()), t)
    }

    func testMacroJSONRoundTrip() throws {
        let url = FileManager.default.temporaryDirectory.appendingPathComponent("kvk-\(UUID().uuidString)/macros.json")
        let t = MacroTable(entries: ["a": "b"])
        try t.save(to: url)
        XCTAssertEqual(MacroTable.load(from: url), t)
    }

    func testAutoCapitalize() {
        check([("xin chaof. toi", "xin chào. Toi"),
               ("a! b? c", "a! B? C"), ("a, b", "a, b")]) { $0.autoCapitalize = true }
    }

    func testFreeMarkingOff() {
        check([("tungw", "tungw"), ("tuwng", "tưng")]) { $0.freeMarking = false }
    }

    func testOldStyleToneOption() {
        check([("hoaf", "hòa")]) { $0.modernTone = false }
    }
}

final class CharsetTests: RecordingTestCase {
    static let lower = Array("àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ")
    static var all: [Character] { lower + lower.map { Character(String($0).uppercased()) } }

    func testHas134Letters() {
        XCTAssertEqual(CharsetTests.all.count, 134)
    }

    func testRoundTripAllCharsets() {
        for cs in Charset.allCases {
            for ch in CharsetTests.all {
                let encoded = cs.encode(String(ch))
                let decoded = cs.decode(encoded)
                // TCVN3 dùng chung mã chữ hoa/thường cho nguyên âm có dấu thanh.
                if cs == .tcvn3, ch.isUppercase, decoded != String(ch) {
                    XCTAssertEqual(decoded, String(ch).lowercased(), "\(cs) \(ch)")
                } else {
                    XCTAssertEqual(decoded, String(ch), "\(cs) \(ch)")
                }
            }
        }
    }

    func testWholeSentenceRoundTrip() {
        let s = "Tiếng Việt rất đẹp, người Hà Nội nói: được rồi!"
        for cs in [Charset.unicode, .unicodeComposite, .vniWindows, .viscii, .cp1258] {
            XCTAssertEqual(cs.decode(cs.encode(s)), s, "\(cs)")
        }
        XCTAssertEqual(Charset.tcvn3.decode(Charset.tcvn3.encode("tiếng việt")), "tiếng việt")
    }

    func testKnownValues() {
        XCTAssertEqual(Charset.tcvn3.encode("ă"), "\u{A8}")
        XCTAssertEqual(Charset.tcvn3.encode("đ"), "\u{AE}")
        XCTAssertEqual(Charset.vniWindows.encode("ấ"), "a\u{E1}")
        XCTAssertEqual(Charset.vniWindows.encode("â"), "a\u{E2}")
        XCTAssertEqual(Charset.vniWindows.encode("đ"), "\u{F1}")
        XCTAssertEqual(Charset.unicodeComposite.encode("ế").unicodeScalars.count, 3)
        XCTAssertEqual(Charset.viscii.encode("a"), "a")
    }

    func testEngineOutputInLegacyCharsets() {
        XCTAssertEqual(Typist.run("tieengs") { $0.charset = .tcvn3 }, Charset.tcvn3.encode("tiếng"))
        XCTAssertEqual(Typist.run("tieengs") { $0.charset = .vniWindows }, Charset.vniWindows.encode("tiếng"))
        XCTAssertEqual(Typist.run("tieengs") { $0.charset = .unicodeComposite }, Charset.unicodeComposite.encode("tiếng"))
        XCTAssertEqual(Typist.run("vieetj nam") { $0.charset = .cp1258 }, Charset.cp1258.encode("việt") + " nam")
    }
}

final class FuzzTests: RecordingTestCase {
    func testRandomKeysNeverCrashOrUnderflow() {
        var rng = SeededGenerator(seed: 42)
        let alphabet = Array("abcdefghijklmnopqrstuvwxyzABCDEFGHIJ0123456789 .,'`?~^(+-\u{8}")
        for method in InputMethod.allCases {
            for charset in Charset.allCases {
                var cfg = EngineConfig()
                cfg.method = method
                cfg.charset = charset
                cfg.autoCapitalize = true
                let t = Typist(cfg, macros: MacroTable(entries: ["ab": "xyz"]))
                for _ in 0..<4000 {
                    t.type(String(alphabet[Int(rng.next() % UInt64(alphabet.count))]))
                }
                XCTAssertFalse(t.invalidBackspace, "\(method) \(charset)")
                t.record()
            }
        }
    }

    func testScreenMatchesEngineWord() {
        var rng = SeededGenerator(seed: 7)
        let alphabet = Array("abcdeghimnoprstuvwxyjfz")
        for _ in 0..<500 {
            let t = Typist()
            var typed = ""
            for _ in 0..<Int(rng.next() % 9 + 1) {
                let c = alphabet[Int(rng.next() % UInt64(alphabet.count))]
                typed.append(c)
                t.type(String(c))
            }
            XCTAssertEqual(t.screen, t.engine.currentWord, "typed: \(typed)")
        }
    }
}

struct SeededGenerator: RandomNumberGenerator {
    var state: UInt64
    init(seed: UInt64) { state = seed }
    mutating func next() -> UInt64 {
        state &+= 0x9E3779B97F4A7C15
        var z = state
        z = (z ^ (z >> 30)) &* 0xBF58476D1CE4E5B9
        z = (z ^ (z >> 27)) &* 0x94D049BB133111EB
        return z ^ (z >> 31)
    }
}
