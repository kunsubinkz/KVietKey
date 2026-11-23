import XCTest
@testable import VietEngine

/// Ghi lại mọi ca test của engine thành JSON để bản C# (windows/) chạy lại cùng dữ liệu.
/// Chỉ hoạt động khi đặt `KVK_EXPORT_VECTORS=<thư mục>`; không đặt thì không làm gì.
/// Chạy: KVK_EXPORT_VECTORS=../../../windows/tests/vectors swift test   (chỉ xuất khi toàn bộ test đã xanh).
enum VectorRecorder {
    static let directory: URL? = ProcessInfo.processInfo.environment["KVK_EXPORT_VECTORS"]
        .flatMap { $0.isEmpty ? nil : URL(fileURLWithPath: $0, isDirectory: true) }

    private final class Group {
        let config: [String: Any]
        let macros: [String: String]
        var cases: [[String: String]] = []
        init(config: [String: Any], macros: [String: String]) { self.config = config; self.macros = macros }
    }

    private struct Key: Hashable { var suite: String; var test: String; var group: String }

    private static var groups: [Key: Group] = [:]
    private static var order: [Key] = []
    private static var extras: [String: [[String: Any]]] = [:]
    private static var current: (suite: String, test: String)?
    private static var registered = false

    private final class Observer: NSObject, XCTestObservation {
        func testCaseWillStart(_ testCase: XCTestCase) {
            let name = testCase.name.trimmingCharacters(in: CharacterSet(charactersIn: "[]-"))
            let test = name.split(whereSeparator: { $0 == " " || $0 == "/" }).last.map(String.init) ?? name
            VectorRecorder.current = (String(describing: type(of: testCase)), test)
        }
        func testBundleDidFinish(_ testBundle: Bundle) { VectorRecorder.flush() }
    }

    static func activate() {
        guard directory != nil, !registered else { return }
        registered = true
        XCTestObservationCenter.shared.addTestObserver(Observer())
    }

    static func configJSON(_ c: EngineConfig) -> [String: Any] {
        [
            "method": c.method.rawValue, "charset": c.charset.rawValue,
            "modernTone": c.modernTone, "freeMarking": c.freeMarking, "spellCheck": c.spellCheck,
            "autoRestore": c.autoRestore, "autoCapitalize": c.autoCapitalize, "allowZFWJ": c.allowZFWJ,
            "wAsU": c.wAsU, "macroEnabled": c.macroEnabled, "macroKeepCase": c.macroKeepCase,
        ]
    }

    private static func serialize(_ object: Any) -> String {
        let data = try! JSONSerialization.data(withJSONObject: object, options: [.sortedKeys, .withoutEscapingSlashes])
        return String(decoding: data, as: UTF8.self)
    }

    /// Một ca gõ: `input` là chuỗi phím (có thể chứa \u{8} = Backspace), `expected` là nội dung màn hình sau cùng.
    static func record(input: String, expected: String, config: EngineConfig, macros: MacroTable) {
        guard directory != nil else { return }
        let cfg = configJSON(config)
        let now = current ?? ("Unknown", "unknown")
        let key = Key(suite: now.suite, test: now.test,
                      group: serialize(cfg) + serialize(macros.entries))
        let group = groups[key] ?? {
            let g = Group(config: cfg, macros: macros.entries)
            groups[key] = g
            order.append(key)
            return g
        }()
        group.cases.append(["input": input, "expected": expected])
    }

    /// Dữ liệu phụ (bảng mã, gõ tắt…) ghi thẳng vào `<name>.json`.
    static func writeExtra(name: String, _ object: [String: Any]) {
        guard let dir = directory else { return }
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let data = try! JSONSerialization.data(withJSONObject: object, options: [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes])
        try! data.write(to: dir.appendingPathComponent("\(name).json"), options: .atomic)
    }

    static func flush() {
        guard let dir = directory else { return }
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        var suites: [String: [String: [[String: Any]]]] = [:]
        for key in order {
            let g = groups[key]!
            suites[key.suite, default: [:]][key.test, default: []].append([
                "config": g.config, "macros": g.macros, "cases": g.cases,
            ])
        }
        for (suite, tests) in suites {
            let object: [String: Any] = [
                "suite": suite,
                "tests": tests.keys.sorted().map { ["name": $0, "groups": tests[$0]!] as [String: Any] },
            ]
            let data = try! JSONSerialization.data(withJSONObject: object, options: [.sortedKeys, .withoutEscapingSlashes])
            try! data.write(to: dir.appendingPathComponent("engine.\(suite).json"), options: .atomic)
        }
    }
}

/// Lớp test cơ sở: đăng ký bộ ghi vector trước ca test đầu tiên của mỗi lớp.
class RecordingTestCase: XCTestCase {
    override class func setUp() {
        super.setUp()
        VectorRecorder.activate()
    }
}
