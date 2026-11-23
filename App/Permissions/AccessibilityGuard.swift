import AppKit
import ApplicationServices

/// Kiểm tra / xin quyền Accessibility.
enum AccessibilityGuard {
    static var isTrusted: Bool { AXIsProcessTrusted() }

    @discardableResult
    static func requestPrompt() -> Bool {
        let options = ["AXTrustedCheckOptionPrompt": true] as CFDictionary
        return AXIsProcessTrustedWithOptions(options)
    }

    static func openSystemSettings() {
        let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!
        NSWorkspace.shared.open(url)
    }
}
