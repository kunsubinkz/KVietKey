import XCTest
@testable import VietEngine

/// Mô phỏng ô văn bản: áp dụng kết quả của engine lên chuỗi màn hình.
final class Typist {
    let engine: Engine
    private(set) var screen = ""
    private(set) var invalidBackspace = false

    init(_ config: EngineConfig = EngineConfig(), macros: MacroTable = MacroTable()) {
        engine = Engine(config: config)
        engine.macros = macros
    }

    private func apply(_ r: EngineResult, original: Character) {
        if r.backspaces > screen.count { invalidBackspace = true }
        screen.removeLast(min(r.backspaces, screen.count))
        screen += r.text
        if r.passthrough { screen.append(original) }
    }

    func type(_ text: String) {
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

    static func run(_ text: String, _ configure: (inout EngineConfig) -> Void = { _ in }) -> String {
        var cfg = EngineConfig()
        configure(&cfg)
        let t = Typist(cfg)
        t.type(text)
        return t.screen
    }
}

class EngineTestCase: XCTestCase {
    func check(_ cases: [(String, String)], _ configure: (inout EngineConfig) -> Void = { _ in },
               file: StaticString = #filePath, line: UInt = #line) {
        for (input, expected) in cases {
            let got = Typist.run(input, configure)
            XCTAssertEqual(got, expected, "input: \(input)", file: file, line: line)
        }
    }
}
