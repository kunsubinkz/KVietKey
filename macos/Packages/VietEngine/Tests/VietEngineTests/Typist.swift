import XCTest
@testable import VietEngine

/// Mô phỏng ô văn bản: áp dụng kết quả của engine lên chuỗi màn hình.
final class Typist {
    let engine: Engine
    private(set) var screen = ""
    private(set) var invalidBackspace = false
    private(set) var typed = ""
    private let macros: MacroTable

    init(_ config: EngineConfig = EngineConfig(), macros: MacroTable = MacroTable()) {
        engine = Engine(config: config)
        engine.macros = macros
        self.macros = macros
    }

    private func apply(_ r: EngineResult, original: Character) {
        if r.backspaces > screen.count { invalidBackspace = true }
        screen.removeLast(min(r.backspaces, screen.count))
        screen += r.text
        if r.passthrough { screen.append(original) }
    }

    func type(_ text: String) {
        typed += text
        for ch in text {
            if ch == "\u{8}" {
                engine.backspace()
                if !screen.isEmpty { screen.removeLast() }
            } else if ch.isLetter && ch.isASCII || engine.config.method.action(for: ch) != nil && !ch.isWhitespace {
                apply(engine.process(ch), original: ch)
            } else {
                apply(engine.processBreak(ch), original: ch)
            }
        }
    }

    /// Ghi chuỗi đã gõ và màn hình hiện tại làm test vector cho bản C# (không làm gì nếu không bật xuất).
    func record() {
        VectorRecorder.record(input: typed, expected: screen, config: engine.config, macros: macros)
    }

    static func run(_ text: String, _ configure: (inout EngineConfig) -> Void = { _ in }) -> String {
        var cfg = EngineConfig()
        configure(&cfg)
        let t = Typist(cfg)
        t.type(text)
        t.record()
        return t.screen
    }
}

class EngineTestCase: RecordingTestCase {
    func check(_ cases: [(String, String)], _ configure: (inout EngineConfig) -> Void = { _ in },
               file: StaticString = #filePath, line: UInt = #line) {
        for (input, expected) in cases {
            let got = Typist.run(input, configure)
            XCTAssertEqual(got, expected, "input: \(input)", file: file, line: line)
        }
    }
}
