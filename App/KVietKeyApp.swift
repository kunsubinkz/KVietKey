import AppKit
import VietEngine

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let prefs = Preferences.shared
    private var controller: InputController!
    private var statusBar: StatusBarController!
    private var settingsWindow: SettingsWindowController!
    private var onboarding: OnboardingWindowController!

    func applicationDidFinishLaunching(_ notification: Notification) {
        controller = InputController(prefs: prefs)
        settingsWindow = SettingsWindowController(prefs: prefs, controller: controller)
        onboarding = OnboardingWindowController(prefs: prefs) { [weak self] in self?.startInput() }
        statusBar = StatusBarController(
            controller: controller,
            openSettings: { [weak self] tab in self?.settingsWindow.show(tab: tab) },
            quit: { NSApp.terminate(nil) }
        )
        controller.onStateChange = { [weak self] in self?.statusBar.refresh() }

        let trusted = AccessibilityGuard.isTrusted
        if trusted { startInput() }
        if !prefs.settings.onboardingDone {
            onboarding.show(startingAt: 1)
        } else if !trusted {
            onboarding.show(startingAt: 2)
        } else {
            warnAboutConflicts()
        }
    }

    /// Mở lại ứng dụng (vd. khi đã ẩn icon menu bar) thì hiện cửa sổ cài đặt.
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        settingsWindow.show(tab: .basic)
        return true
    }

    func applicationWillTerminate(_ notification: Notification) {
        controller.stop()
    }

    private func startInput() {
        if !controller.start() {
            let alert = NSAlert()
            alert.messageText = tr("Không thể bắt phím", "Unable to capture keystrokes")
            alert.informativeText = tr("Hãy cấp quyền Trợ năng (và Giám sát đầu vào nếu được hỏi) cho KVietKey rồi mở lại ứng dụng.",
                                       "Grant KVietKey the Accessibility (and Input Monitoring if asked) permission, then relaunch the app.")
            alert.runModal()
        }
        statusBar.refresh()
    }

    private func warnAboutConflicts() {
        let names = NSWorkspace.shared.runningApplications.compactMap { $0.localizedName }
        let found = AppCompat.conflictingInputApps.filter { c in names.contains { $0.localizedCaseInsensitiveContains(c) } }
        guard !found.isEmpty else { return }
        let alert = NSAlert()
        alert.messageText = tr("Phát hiện bộ gõ khác đang chạy", "Another input method is running")
        alert.informativeText = tr("\(found.joined(separator: ", ")) có thể xung đột với KVietKey. Hãy thoát chúng trước khi gõ.",
                                   "\(found.joined(separator: ", ")) may conflict with KVietKey. Quit it before typing.")
        alert.runModal()
    }
}

@main
enum KVietKeyApp {
    static func main() {
        if CommandLine.arguments.contains("--selftest") {
            exit(SelfTest.run())
        }
        if let i = CommandLine.arguments.firstIndex(of: "--textedit-test"), i + 1 < CommandLine.arguments.count {
            TextEditTest.run(outputPath: CommandLine.arguments[i + 1])
        }
        if let i = CommandLine.arguments.firstIndex(of: "--live-test"), i + 1 < CommandLine.arguments.count {
            LiveTest.run(outputPath: CommandLine.arguments[i + 1])
        }
        if let i = CommandLine.arguments.firstIndex(of: "--render-ui"), i + 1 < CommandLine.arguments.count {
            exit(MainActor.assumeIsolated { UIRender.run(into: CommandLine.arguments[i + 1]) })
        }
        if let i = CommandLine.arguments.firstIndex(of: "--trace-file"), i + 1 < CommandLine.arguments.count {
            // Ghi vết để chẩn đoán (chỉ khi chạy với tham số này): thời điểm (ms) + phím + việc bộ gõ làm. CÓ chứa ký tự đã gõ.
            let path = CommandLine.arguments[i + 1]
            FileManager.default.createFile(atPath: path, contents: nil)
            let handle = FileHandle(forWritingAtPath: path)
            let start = Date()
            InputController.trace = { line in
                let ms = String(format: "%8.1f", Date().timeIntervalSince(start) * 1000)
                handle?.write(Data("\(ms) \(line)\n".utf8))
            }
        }
        let app = NSApplication.shared
        let delegate = AppDelegate()
        app.delegate = delegate
        app.setActivationPolicy(.accessory)
        app.run()
        withExtendedLifetime(delegate) {}
    }
}
