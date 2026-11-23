import AppKit
import ServiceManagement
import VietEngine

/// Chọn tiếng Việt hoặc tiếng Anh cho giao diện theo cài đặt (mặc định theo ngôn ngữ hệ thống).
func tr(_ vi: String, _ en: String) -> String {
    switch Preferences.shared.settings.language {
    case .vi: return vi
    case .en: return en
    case .auto: return Locale.preferredLanguages.first?.hasPrefix("vi") == true ? vi : en
    }
}

enum LaunchAtLogin {
    static var isEnabled: Bool { SMAppService.mainApp.status == .enabled }

    static func set(_ enabled: Bool) {
        do {
            if enabled { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() }
        } catch {
            NSLog("KVietKey: launch at login failed: \(error.localizedDescription)")
        }
    }
}

/// Chuyển mã văn bản trong clipboard.
enum ClipboardConverter {
    struct Conversion {
        let title: String
        let from: Charset
        let to: Charset
    }

    static var conversions: [Conversion] {
        var list: [Conversion] = []
        for cs in [Charset.tcvn3, .vniWindows, .viscii, .cp1258] {
            list.append(Conversion(title: "\(cs.displayName) → Unicode", from: cs, to: .unicode))
            list.append(Conversion(title: "Unicode → \(cs.displayName)", from: .unicode, to: cs))
        }
        list.append(Conversion(title: "Unicode tổ hợp → Unicode dựng sẵn", from: .unicodeComposite, to: .unicode))
        list.append(Conversion(title: "Unicode dựng sẵn → Unicode tổ hợp", from: .unicode, to: .unicodeComposite))
        return list
    }

    @discardableResult
    static func convert(_ c: Conversion) -> Bool {
        let pb = NSPasteboard.general
        guard let text = pb.string(forType: .string), !text.isEmpty else { return false }
        pb.clearContents()
        pb.setString(c.to.encode(c.from.decode(text)), forType: .string)
        return true
    }
}
