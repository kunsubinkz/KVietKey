import AppKit
import CoreGraphics
import SwiftUI
import VietEngine

/// `KVietKey --selftest`: kiểm tra nhanh các thành phần không cần quyền Accessibility
/// (đường đi CGEvent → ký tự → engine → màn hình giả lập, cài đặt, gõ tắt, icon).
enum SelfTest {
    private static var failures = 0

    private static func check(_ condition: Bool, _ name: String) {
        print("\(condition ? "PASS" : "FAIL")  \(name)")
        if !condition { failures += 1 }
    }

    static func run() -> Int32 {
        engineThroughCGEvents()
        settingsPersistence()
        senderChunks()
        hotkeyAndIcon()
        eventTapAvailability()
        print(failures == 0 ? "\nSELFTEST OK" : "\nSELFTEST FAILED (\(failures))")
        return failures == 0 ? 0 : 1
    }

    private static func keyEvent(for ch: Character) -> CGEvent? {
        guard let e = CGEvent(keyboardEventSource: nil, virtualKey: 0, keyDown: true) else { return nil }
        let units = Array(String(ch).utf16)
        e.keyboardSetUnicodeString(stringLength: units.count, unicodeString: units)
        return e
    }

    private static func type(_ text: String, config: EngineConfig = EngineConfig(), macros: MacroTable = MacroTable()) -> String {
        let engine = Engine(config: config)
        engine.macros = macros
        var screen = ""
        for ch in text {
            guard let event = keyEvent(for: ch), let typed = InputController.typedCharacter(event) else { continue }
            let r = engine.process(typed)
            screen.removeLast(min(r.backspaces, screen.count))
            screen += r.text
            if r.passthrough { screen.append(typed) }
        }
        return screen
    }

    private static func engineThroughCGEvents() {
        check(type("tieengs vieetj ") == "tiếng việt ", "CGEvent → Telex: tiếng việt")
        var vni = EngineConfig(); vni.method = .vni
        check(type("Vie6t5 Nam ", config: vni) == "Việt Nam ", "CGEvent → VNI: Việt Nam")
        check(type("windows ") == "windows ", "Tự khôi phục: windows")
        var tcvn = EngineConfig(); tcvn.charset = .tcvn3
        check(type("tieengs", config: tcvn) == Charset.tcvn3.encode("tiếng"), "Bảng mã TCVN3")
        check(type("vn ", macros: MacroTable(entries: ["vn": "Việt Nam"])) == "Việt Nam ", "Gõ tắt")

        let start = DispatchTime.now().uptimeNanoseconds
        let engine = Engine()
        let keys = Array("tieengs vieetj nguwowif ddaay ")
        let rounds = 2000
        for _ in 0..<rounds { for k in keys { _ = engine.process(k) } }
        let perKey = Double(DispatchTime.now().uptimeNanoseconds - start) / Double(rounds * keys.count)
        check(perKey < 1_000_000, String(format: "Độ trễ engine %.1f µs/phím (< 1000 µs)", perKey / 1000))
    }

    private static func settingsPersistence() {
        let suite = UserDefaults(suiteName: "vn.kvietkey.selftest")!
        suite.removePersistentDomain(forName: "vn.kvietkey.selftest")
        let prefs = Preferences(defaults: suite, macroFile: URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("kvietkey-test-macros-\(UUID().uuidString).json"))
        prefs.settings.method = .vni
        prefs.settings.appRules["com.test.app"] = AppRule(mode: .english, charset: .tcvn3)
        let reloaded = Preferences(defaults: suite)
        check(reloaded.settings.method == .vni && reloaded.settings.appRules["com.test.app"]?.charset == .tcvn3,
              "Lưu/đọc cài đặt")
        suite.removePersistentDomain(forName: "vn.kvietkey.selftest")
        check((try? JSONDecoder().decode(Settings.self, from: Data("{}".utf8))) == Settings(), "Cài đặt cũ thiếu khóa → mặc định")
    }

    private static func senderChunks() {
        let text = String(repeating: "ế", count: 40)
        let chunks = EventSender.chunks(of: text)
        check(chunks.joined() == text && chunks.allSatisfy { $0.utf16.count <= 16 }, "Chia chuỗi gửi ≤ 16 UTF-16")
    }

    private static func hotkeyAndIcon() {
        check(Hotkey.controlShift.displayString == "⌃⇧", "Hiển thị phím tắt ⌃⇧")
        check(Hotkey.controlSpace.displayString == "⌃Space", "Hiển thị phím tắt ⌃Space")
        check(StatusBarController.icon(letter: "E", active: false, slashed: false).isTemplate, "Icon menu bar chế độ E dạng template")
    }

    private static func eventTapAvailability() {
        let manager = EventTapManager()
        if manager.start() {
            manager.stop()
            check(true, "Event tap tạo được (đã có quyền)")
        } else {
            print("SKIP  Event tap: chưa được cấp quyền Accessibility (không tính là lỗi)")
        }
    }
}

/// `KVietKey --render-ui <thư mục>`: xuất ảnh PNG các màn hình để so với bản thiết kế (không cần quyền màn hình).
enum UIRender {
    @MainActor static func run(into dir: String) -> Int32 {
        _ = NSApplication.shared
        try? FileManager.default.createDirectory(atPath: dir, withIntermediateDirectories: true)
        let suite = UserDefaults(suiteName: "vn.kvietkey.render")!
        suite.removePersistentDomain(forName: "vn.kvietkey.render")
        let prefs = Preferences(defaults: suite, macroFile: URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("kvietkey-test-macros-\(UUID().uuidString).json"))
        prefs.settings.appRules = [
            "com.apple.Safari": AppRule(mode: .vietnamese),
            "com.google.Chrome": AppRule(mode: .vietnamese),
            "com.tinyspeck.slackmacgap": AppRule(mode: .vietnamese),
            "com.microsoft.VSCode": AppRule(mode: .english),
            "com.apple.Terminal": AppRule(mode: .vietnamese, charset: .tcvn3),
            "com.valvesoftware.steam": AppRule(mode: .off),
        ]
        let savedLanguage = Preferences.shared.settings.language
        Preferences.shared.settings.language = .vi
        defer { Preferences.shared.settings.language = savedLanguage }
        prefs.macros = MacroTable(entries: ["vn": "Việt Nam", "hn": "Hà Nội", "tphcm": "Thành phố Hồ Chí Minh",
                                            "ko": "không", "dc": "được", "sdt": "Số điện thoại"])
        let controller = InputController(prefs: prefs)

        func save<V: View>(_ name: String, _ view: V) {
            // NSHostingView (không phải ImageRenderer) để control AppKit như Picker/TextField được vẽ đúng.
            let host = NSHostingView(rootView: view)
            let size = host.fittingSize
            let window = NSWindow(contentRect: NSRect(origin: .zero, size: size), styleMask: [.borderless],
                                  backing: .buffered, defer: false)
            window.contentView = host
            host.frame = NSRect(origin: .zero, size: size)
            host.layoutSubtreeIfNeeded()
            RunLoop.current.run(until: Date().addingTimeInterval(0.3))
            guard let rep = host.bitmapImageRepForCachingDisplay(in: host.bounds) else { print("FAIL \(name)"); return }
            host.cacheDisplay(in: host.bounds, to: rep)
            guard let png = rep.representation(using: .png, properties: [:]) else { print("FAIL \(name)"); return }
            try? png.write(to: URL(fileURLWithPath: "\(dir)/\(name).png"))
            print("OK   \(name)")
        }

        save("menu", MenuPanelView(prefs: prefs, controller: controller, openSettings: { _ in }, quit: {}, close: {})
            .background(Color(hex: 0xDCE8FF)))
        for step in 1...3 {
            save("onboarding-\(step)", OnboardingView(model: OnboardingModel(prefs: prefs, step: step), prefs: prefs))
        }
        for tab in SettingsTab.allCases {
            let router = SettingsRouter()
            router.tab = tab
            save("settings-\(tab)", SettingsView(prefs: prefs, router: router, controller: controller, scrollable: false))
        }
        suite.removePersistentDomain(forName: "vn.kvietkey.render")
        return 0
    }
}

/// `KVietKey --live-test <file>`: tự gõ phím thật (CGEvent) vào một ô văn bản trong chính app, đi qua event tap,
/// rồi ghi kết quả + vết sự kiện ra file. Cần quyền Accessibility → chạy bằng `open -n -a KVietKey --args --live-test <file>`.
enum LiveTest {
    static func run(outputPath: String) -> Never {
        let app = NSApplication.shared
        app.setActivationPolicy(.regular)
        app.finishLaunching()
        var log = ""
        // NSEvent chỉ được chuyển tới cửa sổ khi bơm hàng đợi sự kiện của NSApplication (RunLoop.run là không đủ).
        func pump(_ seconds: Double) {
            let end = Date().addingTimeInterval(seconds)
            while let e = app.nextEvent(matching: .any, until: end, inMode: .default, dequeue: true) { app.sendEvent(e) }
        }
        InputController.trace = { log += $0 + "\n" }

        let suite = UserDefaults(suiteName: "vn.kvietkey.livetest")!
        suite.removePersistentDomain(forName: "vn.kvietkey.livetest")
        let prefs = Preferences(defaults: suite, macroFile: URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("kvietkey-test-macros-\(UUID().uuidString).json"))
        let controller = InputController(prefs: prefs)

        let scroll = NSTextView.scrollableTextView()
        let textView = scroll.documentView as! NSTextView
        let win = NSWindow(contentRect: NSRect(x: 300, y: 300, width: 520, height: 200), styleMask: [.titled], backing: .buffered, defer: false)
        win.contentView = scroll
        win.level = .floating
        win.makeKeyAndOrderFront(nil)
        app.activate(ignoringOtherApps: true)
        win.makeFirstResponder(textView)
        for _ in 0..<30 where !win.isKeyWindow { pump(0.1) }
        log += "active=\(NSApp.isActive) key=\(win.isKeyWindow) frontmost=\(NSWorkspace.shared.frontmostApplication?.bundleIdentifier ?? "?")\n"
        guard win.isKeyWindow else {
            try? (log + "NOT-ACTIVE: cửa sổ test không nhận được phím, dừng để không gõ nhầm vào app khác\n").write(toFile: outputPath, atomically: true, encoding: .utf8)
            exit(3)
        }

        guard controller.start() else {
            try? "NO-PERMISSION: event tap không tạo được (cần cấp Accessibility cho KVietKey)\n".write(toFile: outputPath, atomically: true, encoding: .utf8)
            exit(2)
        }

        let codes: [Character: CGKeyCode] = ["a":0,"s":1,"d":2,"f":3,"h":4,"g":5,"z":6,"x":7,"c":8,"v":9,"b":11,"q":12,"w":13,"e":14,"r":15,
            "y":16,"t":17,"o":31,"u":32,"i":34,"p":35,"l":37,"j":38,"k":40,"n":45,"m":46," ":49]
        /// Gõ một phím như bàn phím thật: sự kiện mang sẵn ký tự đúng (hoa/thường/ký hiệu) và cờ Shift.
        func postKey(_ code: CGKeyCode, shift: Bool, char: Character?) {
            let src = CGEventSource(stateID: .hidSystemState)
            for down in [true, false] {
                guard let e = CGEvent(keyboardEventSource: src, virtualKey: code, keyDown: down) else { continue }
                e.flags = shift ? .maskShift : []
                if let char {
                    let units = Array(String(char).utf16)
                    e.keyboardSetUnicodeString(stringLength: units.count, unicodeString: units)
                }
                e.post(tap: .cghidEventTap)
            }
        }

        // Chế độ văn bản dài: --keys <file> [--delay-ms N]; ghi màn hình ra <output>.got để so sánh với bản gốc.
        let argv = CommandLine.arguments
        if let k = argv.firstIndex(of: "--keys"), k + 1 < argv.count,
           let tokens = (try? String(contentsOfFile: argv[k + 1], encoding: .utf8))?.split(separator: " ") {
            if argv.contains("--old-tone") { prefs.settings.modernTone = false }
            if let i = argv.firstIndex(of: "--method"), let m = InputMethod(rawValue: argv[i + 1]) { prefs.settings.method = m }
            let charset = argv.firstIndex(of: "--charset").flatMap { Charset(rawValue: argv[$0 + 1]) } ?? .unicode
            prefs.settings.charset = charset
            if let i = argv.firstIndex(of: "--expected"), let exp = try? String(contentsOfFile: argv[i + 1], encoding: .utf8) {
                // Kết quả mong đợi theo bảng mã đích (với Unicode dựng sẵn thì giữ nguyên).
                try? charset.encode(exp).write(toFile: outputPath + ".exp", atomically: true, encoding: .utf8)
            }
            let delay = argv.firstIndex(of: "--delay-ms").flatMap { Double(argv[$0 + 1]) } ?? 8
            pump(0.5)
            for (n, t) in tokens.enumerated() {
                // Giữ focus: nếu cửa sổ test mất key thì lấy lại; không lấy lại được thì dừng, tránh gõ nhầm vào app khác.
                if n % 25 == 0, !win.isKeyWindow || win.firstResponder !== textView {
                    for _ in 0..<20 where !win.isKeyWindow || win.firstResponder !== textView {
                        app.activate(ignoringOtherApps: true); win.makeKeyAndOrderFront(nil); win.makeFirstResponder(textView); pump(0.1)
                    }
                    guard win.isKeyWindow else {
                        try? (log + "ABORT: mất focus ở phím thứ \(n)/\(tokens.count)\n").write(toFile: outputPath, atomically: true, encoding: .utf8)
                        try? textView.string.write(toFile: outputPath + ".got", atomically: true, encoding: .utf8)
                        exit(4)
                    }
                    log += "focus lấy lại ở phím \(n)\n"
                }
                if t.hasPrefix("u"), let v = UInt32(t.dropFirst(), radix: 16), let sc = Unicode.Scalar(v) {
                    let units = Array(String(Character(sc)).utf16)
                    for down in [true, false] {
                        let e = CGEvent(keyboardEventSource: CGEventSource(stateID: .hidSystemState), virtualKey: 0, keyDown: down)!
                        e.flags = []
                        e.keyboardSetUnicodeString(stringLength: units.count, unicodeString: units)
                        e.post(tap: .cghidEventTap)
                    }
                } else {
                    let halves = t.split(separator: "@")
                    let parts = halves[0].split(separator: "+")
                    guard let code = UInt16(parts[0]) else { continue }
                    let char = halves.count > 1 ? UInt32(halves[1], radix: 16).flatMap(Unicode.Scalar.init).map(Character.init) : nil
                    postKey(code, shift: parts.count > 1, char: char)
                }
                pump(delay / 1000)
            }
            pump(0.6)
            try? textView.string.write(toFile: outputPath + ".got", atomically: true, encoding: .utf8)
            try? (log + "STRESS-DONE\n").write(toFile: outputPath, atomically: true, encoding: .utf8)
            exit(0)
        }


        // Bộ test tính năng: gõ tắt, backspace giữa từ, phím tắt chuyển chế độ, cài đặt theo ứng dụng, tự viết hoa.
        if argv.contains("--suite") {
            func type(_ text: String) {
                let punct: [Character: CGKeyCode] = [".": 47, ",": 43, "<": 51]
                let digits: [Character: CGKeyCode] = ["1": 18, "2": 19, "3": 20, "4": 21, "5": 23, "6": 22, "7": 26, "8": 28, "9": 25, "0": 29]
                for ch in text {
                    if let c = punct[ch] { postKey(c, shift: false, char: ch == "<" ? nil : ch) }
                    else if let c = digits[ch] { postKey(c, shift: false, char: ch) }
                    else if ch == "^" { postKey(22, shift: true, char: "^") }
                    else if ch == " " { postKey(49, shift: false, char: " ") }
                    else if let c = codes[Character(ch.lowercased())] { postKey(c, shift: ch.isUppercase, char: ch) }
                    pump(0.012)
                }
                pump(0.15)
            }
            /// Nhấn rồi nhả ⌃⇧ (phím tắt chuyển Việt/Anh mặc định).
            func hotkey() {
                let src = CGEventSource(stateID: .hidSystemState)
                func mod(_ key: CGKeyCode, _ down: Bool, _ flags: CGEventFlags) {
                    guard let e = CGEvent(keyboardEventSource: src, virtualKey: key, keyDown: down) else { return }
                    e.type = .flagsChanged
                    e.flags = flags
                    e.post(tap: .cghidEventTap)
                    pump(0.03)
                }
                mod(59, true, .maskControl)
                mod(56, true, [.maskControl, .maskShift])
                mod(56, false, .maskControl)
                mod(59, false, [])
                pump(0.1)
            }
            let me = controller.frontmostBundleID ?? Bundle.main.bundleIdentifier ?? "vn.kvietkey.app"
            log += "quy tắc theo ứng dụng áp cho: \(me)\n"
            func reset(_ configure: (inout Settings) -> Void = { _ in }) {
                var s = Settings()
                s.onboardingDone = true
                configure(&s)
                controller.setVietnamese(true, userInitiated: false)
                prefs.settings = s
                controller.engine.macros = MacroTable()
                controller.engine.reset()
                textView.string = ""
                for _ in 0..<30 where !win.isKeyWindow || win.firstResponder !== textView {
                    app.activate(ignoringOtherApps: true); win.makeKeyAndOrderFront(nil); win.makeFirstResponder(textView); pump(0.1)
                }
            }
            var passed = 0, failed = 0
            func expect(_ name: String, _ expected: String) {
                let got = textView.string
                if got == expected { passed += 1; log += "PASS \(name)\n" }
                else { failed += 1; log += "FAIL \(name): được \"\(got)\" — mong đợi \"\(expected)\"\n" }
            }
            pump(0.5)

            let macroTable = MacroTable(entries: ["vn": "Việt Nam", "hn": "Hà Nội", "tphcm": "Thành phố Hồ Chí Minh"])
            reset(); controller.engine.macros = macroTable
            type("vn hn tphcm "); expect("Gõ tắt: vn hn tphcm", "Việt Nam Hà Nội Thành phố Hồ Chí Minh ")
            reset(); controller.engine.macros = macroTable
            type("VN "); expect("Gõ tắt giữ hoa: VN → VIỆT NAM", "VIỆT NAM ")
            reset(); controller.engine.macros = macroTable
            type("vn, hn."); expect("Gõ tắt kết thúc bằng dấu câu", "Việt Nam, Hà Nội.")
            reset(); controller.engine.macros = macroTable
            type("vnx "); expect("Gõ tắt không kích hoạt khi gõ dài hơn (vnx)", "vnx ")

            reset(); type("tieengs<<t"); expect("Backspace giữa từ: tiếng ⌫⌫ t → tiết", "tiết")
            reset(); type("chaof<o"); expect("Backspace giữa từ: chào ⌫ o → chào", "chào")
            reset(); type("viet<<<ieetj"); expect("Backspace xoá gần hết rồi gõ lại: việt", "việt")
            reset(); type("tieengs<<<<<<tieengs "); expect("Xoá sạch từ rồi gõ lại", "tiếng ")

            reset(); hotkey(); type("tieengs "); hotkey(); type("tieengs ")
            expect("Phím tắt ⌃⇧: tắt → gõ thô, bật → gõ Việt", "tieengs tiếng ")
            reset(); hotkey(); hotkey(); type("tieengs ")
            expect("Phím tắt ⌃⇧ bấm hai lần → vẫn gõ Việt", "tiếng ")

            reset { $0.appRules[me] = AppRule(mode: .english) }; type("tieengs ")
            expect("Theo ứng dụng: chế độ English → gõ thô", "tieengs ")
            reset { $0.appRules[me] = AppRule(mode: .off) }; type("tieengs ")
            expect("Theo ứng dụng: chế độ Tắt → gõ thô", "tieengs ")
            reset { $0.appRules[me] = AppRule(mode: .off); $0.isVietnamese = false }; hotkey(); type("tieengs ")
            expect("Theo ứng dụng: chế độ Tắt → phím tắt không bật được tiếng Việt", "tieengs ")
            reset { $0.appRules[me] = AppRule(mode: .vietnamese, charset: .tcvn3) }; type("tieengs ")
            expect("Theo ứng dụng: Việt + TCVN3 riêng", Charset.tcvn3.encode("tiếng "))
            reset { $0.appRules[me] = AppRule(mode: .vietnamese) }; type("tieengs ")
            expect("Theo ứng dụng: chế độ Việt", "tiếng ")

            reset { $0.autoCapitalize = true }; type("xin chaof. toif ")
            expect("Tự viết hoa đầu câu", "xin chào. Tòi ")
            reset { $0.modernTone = false }; type("hoaf khoer ")
            expect("Đặt dấu kiểu cũ: hòa khỏe", "hòa khỏe ")
            reset(); type("hoaf khoer ")
            expect("Đặt dấu kiểu mới: hoà khoẻ", "hoà khoẻ ")
            reset { $0.spellCheck = true; $0.autoRestore = true }; type("windows class show ")
            expect("Tự khôi phục từ tiếng Anh", "windows class show ")
            reset { $0.autoRestore = false }; type("windows ")
            expect("Tắt tự khôi phục → giữ kết quả biến đổi", "ưindớ ")
            reset { $0.method = .vni }; type("Vie6t5 Nam ")
            expect("VNI: Vie6t5 Nam", "Việt Nam ")
            reset { $0.method = .viqr }; type("Vie^.t Nam ")
            expect("VIQR: Vie^.t Nam", "Việt Nam ")

            log += failed == 0 ? "SUITE OK (\(passed))\n" : "SUITE FAILED (\(failed)/\(passed + failed))\n"
            try? log.write(toFile: outputPath, atomically: true, encoding: .utf8)
            exit(failed == 0 ? 0 : 1)
        }

        let cases: [(String, String)] = [
            ("chieeuf", "chiều"), ("tieengs", "tiếng"), ("vieetj", "việt"), ("hoaf", "hoà"),
            ("Vieetj Nam", "Việt Nam"), ("windows ", "windows "), ("class ", "class "), ("Vieetj Nam ", "Việt Nam "), ("dduowcj", "được"),
        ]
        func press(_ ch: Character) {
            let shift = ch.isUppercase
            guard let code = codes[Character(ch.lowercased())] else { return }
            for down in [true, false] {
                guard let e = CGEvent(keyboardEventSource: CGEventSource(stateID: .hidSystemState), virtualKey: code, keyDown: down) else { continue }
                e.flags = shift ? .maskShift : []  // ghi đè cả trạng thái CapsLock của máy
                e.post(tap: .cghidEventTap)
            }
        }

        pump(0.5)
        var failures = 0
        for (delayMs, label) in [(60.0, "chậm 60ms"), (8.0, "nhanh 8ms")] {
            for (input, expected) in cases {
                // Máy đang dùng có thể giành focus (thông báo, cửa sổ khác): đảm bảo ô test là key trước mỗi ca.
                for _ in 0..<30 where !win.isKeyWindow || win.firstResponder !== textView {
                    app.activate(ignoringOtherApps: true)
                    win.makeKeyAndOrderFront(nil)
                    win.makeFirstResponder(textView)
                    pump(0.1)
                }
                textView.string = ""
                controller.engine.reset()
                log += "== \(label): \(input)\n"
                pump(0.2)
                for ch in input { press(ch); pump(delayMs / 1000) }
                pump(0.4)
                let got = textView.string
                let ok = got == expected
                if !ok { failures += 1 }
                log += "\(ok ? "PASS" : "FAIL") [\(label)] \(input) → \(got)  (mong đợi \(expected))\n"
            }
        }
        log += failures == 0 ? "LIVETEST OK\n" : "LIVETEST FAILED (\(failures))\n"
        try? log.write(toFile: outputPath, atomically: true, encoding: .utf8)
        exit(failures == 0 ? 0 : 1)
    }
}
