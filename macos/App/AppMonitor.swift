import AppKit

/// Theo dõi ứng dụng đang hoạt động phía trước.
final class AppMonitor {
    private(set) var frontmostBundleID: String?
    private(set) var frontmostName: String?
    var onChange: ((String?) -> Void)?

    init() {
        let front = NSWorkspace.shared.frontmostApplication
        frontmostBundleID = front?.bundleIdentifier
        frontmostName = front?.localizedName
        let center = NSWorkspace.shared.notificationCenter
        center.addObserver(self, selector: #selector(activated(_:)),
                           name: NSWorkspace.didActivateApplicationNotification, object: nil)
        center.addObserver(self, selector: #selector(spaceChanged),
                           name: NSWorkspace.activeSpaceDidChangeNotification, object: nil)
    }

    @objc private func activated(_ note: Notification) {
        guard let app = note.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication else { return }
        guard app.bundleIdentifier != Bundle.main.bundleIdentifier else { return }
        frontmostBundleID = app.bundleIdentifier
        frontmostName = app.localizedName
        onChange?(frontmostBundleID)
    }

    @objc private func spaceChanged() {
        onChange?(frontmostBundleID)
    }

    /// Cửa sổ phía trước của ứng dụng đang active phủ kín một màn hình (full-screen/game).
    static func isFrontmostAppFullscreen() -> Bool {
        guard let app = NSWorkspace.shared.frontmostApplication else { return false }
        let pid = app.processIdentifier
        guard let list = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] else { return false }
        let screens = NSScreen.screens.map { $0.frame.size }
        for info in list {
            guard (info[kCGWindowOwnerPID as String] as? pid_t) == pid,
                  (info[kCGWindowLayer as String] as? Int) == 0,
                  let boundsDict = info[kCGWindowBounds as String] as? NSDictionary,
                  let bounds = CGRect(dictionaryRepresentation: boundsDict) else { continue }
            if screens.contains(where: { abs($0.width - bounds.width) < 1 && abs($0.height - bounds.height) < 1 })
                && bounds.origin.y <= 0.5 {
                return true
            }
        }
        return false
    }
}

/// Hồ sơ tương thích theo bundle ID.
enum AppCompat {
    static let browsers: Set<String> = [
        "com.google.Chrome", "com.google.Chrome.beta", "com.google.Chrome.canary", "org.chromium.Chromium",
        "com.apple.Safari", "com.apple.SafariTechnologyPreview", "com.microsoft.edgemac",
        "org.mozilla.firefox", "company.thebrowser.Browser", "com.brave.Browser",
        "com.operasoftware.Opera", "com.vivaldi.Vivaldi",
    ]

    static let electron: Set<String> = [
        "com.tinyspeck.slackmacgap", "com.hnc.Discord", "com.microsoft.VSCode",
        "com.microsoft.VSCodeInsiders", "notion.id", "com.facebook.archon", "com.todesktop.230313mzl4w4u92",
        "com.microsoft.teams2", "com.figma.Desktop",
    ]

    /// Các bộ gõ khác dễ xung đột khi chạy cùng lúc.
    static let conflictingInputApps = ["EVKey", "OpenKey", "GoTiengViet", "UniKey", "Unikey", "GõKey", "Gõ Nhanh"]
}
