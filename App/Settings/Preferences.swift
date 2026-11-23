import Foundation
import CoreGraphics
import VietEngine

enum AppMode: String, Codable, CaseIterable {
    case auto, vietnamese, english, off
}

/// Cấu hình riêng cho từng ứng dụng (theo bundle ID).
struct AppRule: Codable, Equatable {
    var mode: AppMode = .auto
    var charset: Charset?
}

/// Phím tắt chuyển Việt ⇄ Anh: chỉ phím bổ trợ (keyCode == nil) hoặc tổ hợp bổ trợ + phím.
struct Hotkey: Codable, Equatable {
    var modifiers: UInt64
    var keyCode: Int?

    static let relevantMask: UInt64 = CGEventFlags.maskControl.rawValue | CGEventFlags.maskShift.rawValue
        | CGEventFlags.maskAlternate.rawValue | CGEventFlags.maskCommand.rawValue

    static let controlShift = Hotkey(modifiers: CGEventFlags.maskControl.rawValue | CGEventFlags.maskShift.rawValue, keyCode: nil)
    static let commandShift = Hotkey(modifiers: CGEventFlags.maskCommand.rawValue | CGEventFlags.maskShift.rawValue, keyCode: nil)
    static let optionShift = Hotkey(modifiers: CGEventFlags.maskAlternate.rawValue | CGEventFlags.maskShift.rawValue, keyCode: nil)
    static let controlSpace = Hotkey(modifiers: CGEventFlags.maskControl.rawValue, keyCode: KeyCode.space)

    var displayString: String {
        var s = ""
        if modifiers & CGEventFlags.maskControl.rawValue != 0 { s += "⌃" }
        if modifiers & CGEventFlags.maskAlternate.rawValue != 0 { s += "⌥" }
        if modifiers & CGEventFlags.maskShift.rawValue != 0 { s += "⇧" }
        if modifiers & CGEventFlags.maskCommand.rawValue != 0 { s += "⌘" }
        if let k = keyCode { s += KeyCode.name(for: k) }
        return s
    }
}

enum AppLanguage: String, Codable, CaseIterable {
    case auto, vi, en
}

struct Settings: Codable, Equatable {
    var method: InputMethod = .telex
    var charset: Charset = .unicode
    var modernTone = true
    var freeMarking = true
    var spellCheck = true
    var autoRestore = true
    var autoCapitalize = false
    var allowZFWJ = false
    var wAsU = true
    var macroEnabled = true
    var macroKeepCase = true

    var isVietnamese = true
    var hotkey: Hotkey = .controlShift
    var launchAtLogin = false
    var soundOnToggle = false
    var rememberAppState = true
    var appRules: [String: AppRule] = [:]
    var lastAppState: [String: Bool] = [:]
    var disableInFullscreen = false
    var browserAutocompleteFix = true
    var electronDelayMicros = 1500
    var language: AppLanguage = .auto
    var debugLog = false
    var onboardingDone = false
    var showMenuBarIcon = true

    init() {}

    init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        func v<T: Decodable>(_ key: CodingKeys, _ def: T) -> T { (try? c.decodeIfPresent(T.self, forKey: key)) ?? def }
        method = v(.method, method)
        charset = v(.charset, charset)
        modernTone = v(.modernTone, modernTone)
        freeMarking = v(.freeMarking, freeMarking)
        spellCheck = v(.spellCheck, spellCheck)
        autoRestore = v(.autoRestore, autoRestore)
        autoCapitalize = v(.autoCapitalize, autoCapitalize)
        allowZFWJ = v(.allowZFWJ, allowZFWJ)
        wAsU = v(.wAsU, wAsU)
        macroEnabled = v(.macroEnabled, macroEnabled)
        macroKeepCase = v(.macroKeepCase, macroKeepCase)
        isVietnamese = v(.isVietnamese, isVietnamese)
        hotkey = v(.hotkey, hotkey)
        launchAtLogin = v(.launchAtLogin, launchAtLogin)
        soundOnToggle = v(.soundOnToggle, soundOnToggle)
        rememberAppState = v(.rememberAppState, rememberAppState)
        appRules = v(.appRules, appRules)
        lastAppState = v(.lastAppState, lastAppState)
        disableInFullscreen = v(.disableInFullscreen, disableInFullscreen)
        browserAutocompleteFix = v(.browserAutocompleteFix, browserAutocompleteFix)
        electronDelayMicros = v(.electronDelayMicros, electronDelayMicros)
        language = v(.language, language)
        debugLog = v(.debugLog, debugLog)
        onboardingDone = v(.onboardingDone, onboardingDone)
        showMenuBarIcon = v(.showMenuBarIcon, showMenuBarIcon)
    }

    func engineConfig(charsetOverride: Charset? = nil) -> EngineConfig {
        var cfg = EngineConfig()
        cfg.method = method
        cfg.charset = charsetOverride ?? charset
        cfg.modernTone = modernTone
        cfg.freeMarking = freeMarking
        cfg.spellCheck = spellCheck
        cfg.autoRestore = autoRestore
        cfg.autoCapitalize = autoCapitalize
        cfg.allowZFWJ = allowZFWJ
        cfg.wAsU = wAsU
        cfg.macroEnabled = macroEnabled
        cfg.macroKeepCase = macroKeepCase
        return cfg
    }
}

/// Lưu cài đặt vào UserDefaults và gõ tắt vào `~/Library/Application Support/KVietKey/macros.json`.
final class Preferences: ObservableObject {
    static let shared = Preferences()

    private static let key = "KVietKey.settings.v1"
    private let defaults: UserDefaults
    /// File gõ tắt; các chế độ test dùng file tạm để không ghi đè dữ liệu thật của người dùng.
    private let macroFile: URL

    @Published var settings: Settings {
        didSet {
            guard settings != oldValue else { return }
            persist()
            onChange?(oldValue, settings)
        }
    }

    @Published var macros: MacroTable {
        didSet {
            guard macros != oldValue else { return }
            try? macros.save(to: macroFile)
            onMacrosChange?()
        }
    }

    var onChange: ((Settings, Settings) -> Void)?
    var onMacrosChange: (() -> Void)?

    static var macroURL: URL {
        FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("KVietKey/macros.json")
    }

    init(defaults: UserDefaults = .standard, macroFile: URL = Preferences.macroURL) {
        self.defaults = defaults
        self.macroFile = macroFile
        if let data = defaults.data(forKey: Preferences.key),
           let s = try? JSONDecoder().decode(Settings.self, from: data) {
            settings = s
        } else {
            settings = Settings()
        }
        macros = MacroTable.load(from: macroFile)
    }

    private func persist() {
        if let data = try? JSONEncoder().encode(settings) {
            defaults.set(data, forKey: Preferences.key)
        }
    }
}
