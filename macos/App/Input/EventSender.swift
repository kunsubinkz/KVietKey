import Foundation
import ApplicationServices
import CoreGraphics

/// Gửi phím tổng hợp (Backspace + chuỗi Unicode) vào luồng sự kiện.
final class EventSender {
    /// Gắn vào mọi sự kiện do KVietKey tạo ra để bỏ qua khi bị bắt lại.
    static let magic: Int64 = 0x4B56_4B31
    /// Mã phím cho sự kiện mang chuỗi Unicode. Phải là phím có thật (TextEdit bỏ qua mã không tồn tại) nhưng:
    /// - không có bảng dấu PressAndHold (a, e, o, u, i, y, c, n, s, l, z… có), và
    /// - không đang bị người dùng giữ. Khi gõ nhanh người dùng thường còn giữ phím trước (vd. còn giữ `a` lúc bấm `f`);
    ///   nếu gửi bằng mã của phím đang giữ, macOS coi là lặp phím và bỏ đi → "laf" chỉ còn "l".
    static let unicodeKeyCandidates: [CGKeyCode] = [38, 40, 4, 5, 3, 11, 46, 35, 17, 9, 7, 15]  // j k h g f b m p t v x r

    static func freeUnicodeKeyCode() -> CGKeyCode {
        unicodeKeyCandidates.first { !CGEventSource.keyState(.hidSystemState, key: $0) } ?? unicodeKeyCandidates[0]
    }

    private let source = CGEventSource(stateID: .privateState)

    struct Options {
        /// Độ trễ (µs) giữa các bước gửi, cho ứng dụng xử lý sự kiện chậm (Electron).
        var delayMicros = 0
        /// Xóa phần gợi ý tự hoàn thành (đang được bôi chọn) trước khi gửi Backspace.
        var clearAutocompleteSelection = false
    }

    func send(backspaces: Int, text: String, proxy: CGEventTapProxy?, options: Options) {
        if backspaces > 0, options.clearAutocompleteSelection, Self.selectionLength() ?? 0 > 0 {
            key(KeyCode.forwardDelete, proxy: proxy)
        }
        for _ in 0..<backspaces {
            key(KeyCode.delete, proxy: proxy)
            if options.delayMicros > 0 { usleep(UInt32(options.delayMicros)) }
        }
        if backspaces > 0, options.delayMicros > 0 { usleep(UInt32(options.delayMicros)) }
        for chunk in Self.chunks(of: text) {
            unicode(chunk, proxy: proxy)
            if options.delayMicros > 0 { usleep(UInt32(options.delayMicros)) }
        }
    }

    /// Phát lại một phím gốc (sau phần thay thế) với đúng mã phím và cờ bổ trợ.
    func repost(_ original: CGEvent, proxy: CGEventTapProxy?) {
        guard let copy = original.copy() else { return }
        copy.setIntegerValueField(.eventSourceUserData, value: Self.magic)
        post(copy, proxy: proxy)
    }

    private func key(_ code: Int, proxy: CGEventTapProxy?) {
        for down in [true, false] {
            guard let e = CGEvent(keyboardEventSource: source, virtualKey: CGKeyCode(code), keyDown: down) else { continue }
            e.flags = []
            e.setIntegerValueField(.eventSourceUserData, value: Self.magic)
            post(e, proxy: proxy)
        }
    }

    private func unicode(_ string: String, proxy: CGEventTapProxy?) {
        let units = Array(string.utf16)
        let keyCode = Self.freeUnicodeKeyCode()
        for down in [true, false] {
            guard let e = CGEvent(keyboardEventSource: source, virtualKey: keyCode, keyDown: down) else { continue }
            e.flags = []
            e.keyboardSetUnicodeString(stringLength: units.count, unicodeString: units)
            e.setIntegerValueField(.eventSourceUserData, value: Self.magic)
            post(e, proxy: proxy)
        }
    }

    private func post(_ event: CGEvent, proxy: CGEventTapProxy?) {
        if let proxy {
            event.tapPostEvent(proxy)
        } else {
            event.post(tap: .cgSessionEventTap)
        }
    }

    /// Mỗi sự kiện bàn phím chỉ mang được tối đa 20 đơn vị UTF-16.
    static func chunks(of text: String, maxUnits: Int = 16) -> [String] {
        var result: [String] = []
        var current = ""
        var count = 0
        for ch in text {
            let n = String(ch).utf16.count
            if count + n > maxUnits, !current.isEmpty {
                result.append(current)
                current = ""
                count = 0
            }
            current.append(ch)
            count += n
        }
        if !current.isEmpty { result.append(current) }
        return result
    }

    /// Độ dài vùng đang được bôi chọn trong ô nhập đang focus (nil nếu không đọc được).
    static func selectionLength() -> Int? {
        let system = AXUIElementCreateSystemWide()
        AXUIElementSetMessagingTimeout(system, 0.05)
        var focused: CFTypeRef?
        guard AXUIElementCopyAttributeValue(system, kAXFocusedUIElementAttribute as CFString, &focused) == .success,
              let element = focused, CFGetTypeID(element) == AXUIElementGetTypeID() else { return nil }
        var value: CFTypeRef?
        guard AXUIElementCopyAttributeValue(element as! AXUIElement, kAXSelectedTextRangeAttribute as CFString, &value) == .success,
              let range = value, CFGetTypeID(range) == AXValueGetTypeID() else { return nil }
        var cfRange = CFRange()
        guard AXValueGetValue(range as! AXValue, .cfRange, &cfRange) else { return nil }
        return cfRange.length
    }
}
