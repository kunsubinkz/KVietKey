import AppKit
import ApplicationServices
import VietEngine

/// `KVietKey --textedit-test <file> --keys <keys> [--expected <file>] [--delay-ms N] [--rollover-ms N] [--jitter-ms N] [--old-tone]`
/// Gõ chuỗi phím vào TextEdit thật (tạo tài liệu mới bằng ⌘N), đi qua event tap của chính app, rồi đọc lại nội dung bằng
/// Accessibility. `--rollover-ms N`: nhấn phím kế tiếp N ms sau phím trước khi phím trước được nhả (mô phỏng gõ nhanh chồng phím).
enum TextEditTest {
    static func run(outputPath: String) -> Never {
        let argv = CommandLine.arguments
        func arg(_ name: String) -> String? { argv.firstIndex(of: name).flatMap { $0 + 1 < argv.count ? argv[$0 + 1] : nil } }
        func fail(_ message: String, code: Int32) -> Never {
            try? message.write(toFile: outputPath, atomically: true, encoding: .utf8)
            exit(code)
        }
        let app = NSApplication.shared
        app.setActivationPolicy(.accessory)
        app.finishLaunching()
        func pump(_ seconds: Double) { RunLoop.main.run(until: Date().addingTimeInterval(seconds)) }

        guard AXIsProcessTrusted() else { fail("NO-PERMISSION: chưa có quyền Accessibility\n", code: 2) }

        let suite = UserDefaults(suiteName: "vn.kvietkey.textedit")!
        suite.removePersistentDomain(forName: "vn.kvietkey.textedit")
        let macroFile = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("kvietkey-te-macros-\(UUID().uuidString).json")
        let prefs = Preferences(defaults: suite, macroFile: macroFile)
        var s = Settings(); s.onboardingDone = true
        if argv.contains("--old-tone") { s.modernTone = false }
        if let m = arg("--method").flatMap(InputMethod.init(rawValue:)) { s.method = m }
        let charset = arg("--charset").flatMap(Charset.init(rawValue:)) ?? .unicode
        s.charset = charset
        prefs.settings = s
        let controller = InputController(prefs: prefs)
        var trace = ""
        var log = ""
        if argv.contains("--trace") { InputController.trace = { trace += $0 + "\n" } }
        guard controller.start() else { fail("NO-PERMISSION: event tap không tạo được\n", code: 2) }

        if let path = arg("--expected"), let exp = try? String(contentsOfFile: path, encoding: .utf8) {
            try? charset.encode(exp).write(toFile: outputPath + ".exp", atomically: true, encoding: .utf8)
        }
        guard let keysPath = arg("--keys"),
              let tokens = (try? String(contentsOfFile: keysPath, encoding: .utf8))?.split(separator: " ") else {
            fail("LỖI: thiếu --keys\n", code: 5)
        }
        let delay = arg("--delay-ms").flatMap(Double.init) ?? 30
        let rollover = arg("--rollover-ms").flatMap(Double.init)
        let jitter = arg("--jitter-ms").flatMap(Double.init) ?? 0

        // Đưa TextEdit lên trước.
        let bundle = "com.apple.TextEdit"
        func frontIsTextEdit() -> Bool { NSWorkspace.shared.frontmostApplication?.bundleIdentifier == bundle }
        let cfg = NSWorkspace.OpenConfiguration(); cfg.activates = true
        NSWorkspace.shared.openApplication(at: URL(fileURLWithPath: "/System/Applications/TextEdit.app"), configuration: cfg) { _, _ in }
        for _ in 0..<50 where !frontIsTextEdit() { pump(0.1) }
        guard frontIsTextEdit(), let te = NSRunningApplication.runningApplications(withBundleIdentifier: bundle).first else {
            fail("ABORT: không đưa được TextEdit lên trước\n", code: 3)
        }
        let axApp = AXUIElementCreateApplication(te.processIdentifier)

        let src = CGEventSource(stateID: .hidSystemState)
        func post(_ code: CGKeyCode, down: Bool, flags: CGEventFlags, char: Character?) {
            guard let e = CGEvent(keyboardEventSource: src, virtualKey: code, keyDown: down) else { return }
            e.flags = flags
            if let char {
                let units = Array(String(char).utf16)
                e.keyboardSetUnicodeString(stringLength: units.count, unicodeString: units)
            }
            e.post(tap: .cghidEventTap)
        }
        func tap(_ code: CGKeyCode, flags: CGEventFlags = []) {
            post(code, down: true, flags: flags, char: nil); post(code, down: false, flags: flags, char: nil)
        }
        func focusedTextArea() -> AXUIElement? {
            var focused: CFTypeRef?
            guard AXUIElementCopyAttributeValue(axApp, kAXFocusedUIElementAttribute as CFString, &focused) == .success,
                  let f = focused else { return nil }
            let el = f as! AXUIElement
            var role: CFTypeRef?
            AXUIElementCopyAttributeValue(el, kAXRoleAttribute as CFString, &role)
            return (role as? String) == "AXTextArea" ? el : nil
        }
        func readText() -> String? {
            guard let el = focusedTextArea() else { return nil }
            var value: CFTypeRef?
            guard AXUIElementCopyAttributeValue(el, kAXValueAttribute as CFString, &value) == .success else { return nil }
            return value as? String
        }

        // Tài liệu mới để không đụng vào nội dung đang có.
        tap(45, flags: .maskCommand)  // ⌘N
        pump(1.2)
        for _ in 0..<20 where focusedTextArea() == nil { pump(0.2) }
        guard focusedTextArea() != nil else { fail("ABORT: ô soạn thảo của TextEdit không nhận focus (có thể đang hiện hộp thoại)\n", code: 3) }
        controller.engine.reset()
        pump(0.3)

        var pendingUp: (CGKeyCode, CGEventFlags, Character?)?
        func nap(_ ms: Double) { pump(max(0, ms + (jitter > 0 ? Double.random(in: -jitter...jitter) : 0)) / 1000) }

        for (n, t) in tokens.enumerated() {
            if n % 10 == 0 && !frontIsTextEdit() {
                // Có thể một cửa sổ khác giành focus (thông báo…): thử lấy lại TextEdit trước khi bỏ cuộc.
                if let p = pendingUp { post(p.0, down: false, flags: p.1, char: p.2); pendingUp = nil }
                for _ in 0..<30 where !frontIsTextEdit() {
                    NSWorkspace.shared.openApplication(at: URL(fileURLWithPath: "/System/Applications/TextEdit.app"), configuration: cfg) { _, _ in }
                    pump(0.2)
                }
                guard frontIsTextEdit() else {
                    let front = NSWorkspace.shared.frontmostApplication?.bundleIdentifier ?? "?"
                    try? (readText() ?? "").write(toFile: outputPath + ".got", atomically: true, encoding: .utf8)
                    fail("ABORT: TextEdit mất focus ở phím thứ \(n)/\(tokens.count), app phía trước: \(front)\n", code: 4)
                }
                _ = focusedTextArea()
                log += "focus lấy lại ở phím \(n)\n"
            }
            var code: CGKeyCode = 0, flags: CGEventFlags = [], char: Character?
            if t.hasPrefix("u"), let v = UInt32(t.dropFirst(), radix: 16), let sc = Unicode.Scalar(v) {
                char = Character(sc)
            } else {
                let halves = t.split(separator: "@")
                let parts = halves[0].split(separator: "+")
                guard let c = UInt16(parts[0]) else { continue }
                code = c
                if parts.count > 1 { flags = .maskShift }
                char = halves.count > 1 ? UInt32(halves[1], radix: 16).flatMap(Unicode.Scalar.init).map(Character.init) : nil
            }
            post(code, down: true, flags: flags, char: char)
            if let r = rollover {
                // Chồng phím: phím trước chỉ được nhả sau khi phím này đã nhấn.
                if let p = pendingUp { nap(r); post(p.0, down: false, flags: p.1, char: p.2) }
                pendingUp = (code, flags, char)
                nap(max(0, delay - r))
            } else {
                post(code, down: false, flags: flags, char: char)
                nap(delay)
            }
        }
        if let p = pendingUp { post(p.0, down: false, flags: p.1, char: p.2) }
        pump(1.5)
        let got = readText() ?? "<không đọc được>"
        try? got.write(toFile: outputPath + ".got", atomically: true, encoding: .utf8)
        if argv.contains("--trace") { try? trace.write(toFile: outputPath + ".trace", atomically: true, encoding: .utf8) }
        // Dọn: xoá nội dung vừa gõ để lại tài liệu trống.
        tap(0, flags: .maskCommand); pump(0.1); tap(51); pump(0.2)
        try? (log + "TEXTEDIT-DONE \(tokens.count) phím\n").write(toFile: outputPath, atomically: true, encoding: .utf8)
        exit(0)
    }
}
