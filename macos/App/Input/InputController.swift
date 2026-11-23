import AppKit
import Carbon.HIToolbox
import VietEngine
import os

/// Điều phối: nhận sự kiện từ event tap → gọi engine → gửi kết quả; quản lý trạng thái Việt/Anh.
final class InputController: ObservableObject {
    let prefs: Preferences
    let engine = Engine()
    let tap = EventTapManager()
    let monitor = AppMonitor()
    private let sender = EventSender()
    private let logger = Logger(subsystem: "vn.kvietkey.app", category: "input")

    /// Trạng thái gõ tiếng Việt hiện tại (theo ứng dụng đang dùng).
    private(set) var vietnamese: Bool
    private(set) var secureInput = false
    private(set) var fullscreenSuspended = false
    var onStateChange: (() -> Void)?
    /// Chỉ dùng cho `--live-test`: ghi vết từng sự kiện (không dùng khi chạy thường).
    static var trace: ((String) -> Void)?

    private func notify() {
        objectWillChange.send()
        onStateChange?()
    }

    private var hotkeyArmed = false

    init(prefs: Preferences) {
        self.prefs = prefs
        vietnamese = prefs.settings.isVietnamese
        tap.handler = { [unowned self] proxy, type, event in handle(proxy: proxy, type: type, event: event) }
        monitor.onChange = { [unowned self] _ in appChanged() }
        prefs.onChange = { [unowned self] old, new in settingsChanged(old: old, new: new) }
        prefs.onMacrosChange = { [unowned self] in engine.macros = prefs.macros }
        applyConfig()
        appChanged()
    }

    // MARK: Trạng thái

    var frontmostBundleID: String? { monitor.frontmostBundleID }

    private var currentRule: AppRule? {
        frontmostBundleID.flatMap { prefs.settings.appRules[$0] }
    }

    /// Đang thực sự biến đổi phím (bật tiếng Việt, không bị tạm dừng).
    var isActive: Bool { vietnamese && !secureInput && !fullscreenSuspended && currentRule?.mode != .off }

    func start() -> Bool { tap.start() }
    func stop() { tap.stop(); engine.reset() }

    func setVietnamese(_ on: Bool, userInitiated: Bool = true) {
        if userInitiated, currentRule?.mode == .off { return }
        guard vietnamese != on else { return }
        vietnamese = on
        engine.reset()
        var s = prefs.settings
        s.isVietnamese = on
        if userInitiated, s.rememberAppState, let id = frontmostBundleID { s.lastAppState[id] = on }
        prefs.settings = s
        if userInitiated, s.soundOnToggle { NSSound(named: on ? "Pop" : "Tink")?.play() }
        notify()
    }

    func toggle() { setVietnamese(!vietnamese) }

    private func applyConfig() {
        engine.config = prefs.settings.engineConfig(charsetOverride: currentRule?.charset)
        engine.macros = prefs.macros
        engine.reset()
    }

    private func settingsChanged(old: Settings, new: Settings) {
        applyConfig()
        if old.appRules != new.appRules { appChanged() }
        notify()
    }

    private func appChanged() {
        engine.reset()
        let s = prefs.settings
        if let id = frontmostBundleID {
            switch s.appRules[id]?.mode ?? .auto {
            case .vietnamese: vietnamese = true
            case .english, .off: vietnamese = false
            case .auto:
                if s.rememberAppState { vietnamese = s.lastAppState[id] ?? true }
            }
        }
        fullscreenSuspended = s.disableInFullscreen && AppMonitor.isFrontmostAppFullscreen()
        applyConfig()
        notify()
    }

    // MARK: Xử lý sự kiện

    private func handle(proxy: CGEventTapProxy, type: CGEventType, event: CGEvent) -> Unmanaged<CGEvent>? {
        let pass = Unmanaged.passUnretained(event)
        if event.getIntegerValueField(.eventSourceUserData) == EventSender.magic {
            Self.trace?("SEEN-MAGIC type=\(type.rawValue) key=\(event.getIntegerValueField(.keyboardEventKeycode))")
            return pass
        }

        switch type {
        case .leftMouseDown, .rightMouseDown, .otherMouseDown:
            engine.reset()
            return pass
        case .flagsChanged:
            handleFlagsChanged(event)
            return pass
        case .keyDown:
            if Self.trace != nil, event.getIntegerValueField(.keyboardEventAutorepeat) != 0 {
                Self.trace?("AUTOREPEAT key=\(event.getIntegerValueField(.keyboardEventKeycode))")
            }
            return handleKeyDown(proxy: proxy, event: event)
        case .keyUp:
            Self.trace?("up   key=\(event.getIntegerValueField(.keyboardEventKeycode))")
            return pass
        default:
            return pass
        }
    }

    private func handleFlagsChanged(_ event: CGEvent) {
        let hotkey = prefs.settings.hotkey
        guard hotkey.keyCode == nil else { return }
        let current = event.flags.rawValue & Hotkey.relevantMask
        if current == hotkey.modifiers {
            hotkeyArmed = true
        } else {
            if hotkeyArmed { toggle() }
            hotkeyArmed = false
        }
    }

    private func handleKeyDown(proxy: CGEventTapProxy, event: CGEvent) -> Unmanaged<CGEvent>? {
        let pass = Unmanaged.passUnretained(event)
        let keyCode = Int(event.getIntegerValueField(.keyboardEventKeycode))
        let flags = event.flags
        hotkeyArmed = false

        let hotkey = prefs.settings.hotkey
        if let hk = hotkey.keyCode, keyCode == hk, flags.rawValue & Hotkey.relevantMask == hotkey.modifiers {
            toggle()
            return nil
        }

        let secure = IsSecureEventInputEnabled()
        if secure != secureInput {
            secureInput = secure
            engine.reset()
            notify()
        }
        guard isActive else {
            Self.trace?("down key=\(keyCode) bỏ qua (isActive=false: viet=\(vietnamese) secure=\(secureInput))")
            return pass
        }

        if flags.contains(.maskCommand) || flags.contains(.maskControl) || flags.contains(.maskAlternate) {
            engine.reset()
            return pass
        }
        if keyCode == KeyCode.delete {
            engine.backspace()
            return pass
        }
        if KeyCode.navigation.contains(keyCode) {
            engine.reset()
            return pass
        }
        if KeyCode.wordBreakers.contains(keyCode) {
            let ch: Character = keyCode == KeyCode.tab ? "\t" : "\n"
            return emit(engine.processBreak(ch), event: event, proxy: proxy)
        }
        guard let ch = Self.typedCharacter(event) else {
            Self.trace?("REAL key=\(keyCode) no-char → reset")
            engine.reset()
            return pass
        }
        let result = engine.process(ch)
        Self.trace?("down key=\(keyCode) ch=\(ch) → xoá=\(result.backspaces) gửi=\(result.text) choQua=\(result.passthrough) từ=\(engine.currentWord) app=\(frontmostBundleID ?? "?")")
        return emit(result, event: event, proxy: proxy)
    }

    private func emit(_ result: EngineResult, event: CGEvent, proxy: CGEventTapProxy) -> Unmanaged<CGEvent>? {
        if result.isEmpty { return result.passthrough ? Unmanaged.passUnretained(event) : nil }
        sender.send(backspaces: result.backspaces, text: result.text, proxy: proxy, options: sendOptions())
        if prefs.settings.debugLog {
            logger.debug("emit backspaces=\(result.backspaces) chars=\(result.text.count) passthrough=\(result.passthrough)")
        }
        if result.passthrough { sender.repost(event, proxy: proxy) }
        return nil
    }

    private func sendOptions() -> EventSender.Options {
        var options = EventSender.Options()
        if let id = frontmostBundleID {
            if AppCompat.browsers.contains(id) { options.clearAutocompleteSelection = prefs.settings.browserAutocompleteFix }
            if AppCompat.electron.contains(id) { options.delayMicros = prefs.settings.electronDelayMicros }
        }
        return options
    }

    /// Ký tự thực sự được tạo ra bởi phím (đã tính bố cục bàn phím, Shift, CapsLock).
    static func typedCharacter(_ event: CGEvent) -> Character? {
        var length = 0
        var buffer = [UniChar](repeating: 0, count: 8)
        event.keyboardGetUnicodeString(maxStringLength: 8, actualStringLength: &length, unicodeString: &buffer)
        guard length > 0 else { return nil }
        let string = String(utf16CodeUnits: buffer, count: length)
        guard string.count == 1, let ch = string.first,
              let scalar = ch.unicodeScalars.first, scalar.value >= 0x20, scalar.value != 0x7F,
              !(0xF700...0xF8FF).contains(scalar.value) else { return nil }
        return ch
    }
}
