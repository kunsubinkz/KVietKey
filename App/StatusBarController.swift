import AppKit
import SwiftUI
import VietEngine

/// Icon menu bar (V/E) và panel chính.
final class StatusBarController: NSObject {
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
    private let controller: InputController
    private var panel: MenuPanelController!

    init(controller: InputController,
         openSettings: @escaping (SettingsTab) -> Void,
         quit: @escaping () -> Void) {
        self.controller = controller
        super.init()
        panel = MenuPanelController { [controller] close in
            AnyView(MenuPanelView(prefs: controller.prefs, controller: controller,
                                  openSettings: openSettings, quit: quit, close: close))
        }
        statusItem.button?.target = self
        statusItem.button?.action = #selector(clicked(_:))
        statusItem.button?.sendAction(on: [.leftMouseUp, .rightMouseUp])
        refresh()
    }

    func refresh() {
        statusItem.isVisible = controller.prefs.settings.showMenuBarIcon
        let vi = controller.vietnamese
        statusItem.button?.image = Self.icon(letter: vi ? "V" : "E", active: vi, slashed: controller.secureInput)
        let method = controller.prefs.settings.method.displayName
        var tip = vi ? tr("Đang gõ tiếng Việt", "Vietnamese on") + " (\(method))" : tr("Đang gõ tiếng Anh", "English")
        if controller.secureInput { tip += " — " + tr("tạm dừng (ô nhập bảo mật)", "paused (secure input)") }
        if controller.fullscreenSuspended { tip += " — " + tr("tạm dừng (toàn màn hình)", "paused (full screen)") }
        statusItem.button?.toolTip = tip
    }

    /// V: chip xanh chữ trắng (màu thương hiệu). E: chip viền dạng template để tự đổi theo sáng/tối.
    static func icon(letter: String, active: Bool, slashed: Bool) -> NSImage {
        let size = NSSize(width: 26, height: 18)
        let image = NSImage(size: size, flipped: false) { rect in
            let chip = NSBezierPath(roundedRect: rect.insetBy(dx: 1, dy: 1), xRadius: 5, yRadius: 5)
            let attrs: [NSAttributedString.Key: Any] = [
                .font: NSFont.systemFont(ofSize: 12, weight: .bold),
                .foregroundColor: active ? NSColor.white : NSColor.black,
            ]
            if active {
                NSColor(red: 0x1F / 255, green: 0x5B / 255, blue: 1, alpha: 1).setFill()
                chip.fill()
            } else {
                chip.lineWidth = 1.4
                NSColor.black.setStroke()
                chip.stroke()
            }
            let str = NSAttributedString(string: letter, attributes: attrs)
            let s = str.size()
            str.draw(at: NSPoint(x: (rect.width - s.width) / 2, y: (rect.height - s.height) / 2))
            if slashed {
                let line = NSBezierPath()
                line.move(to: NSPoint(x: 4, y: 3))
                line.line(to: NSPoint(x: rect.width - 4, y: rect.height - 3))
                line.lineWidth = 1.8
                (active ? NSColor.white : NSColor.black).setStroke()
                line.stroke()
            }
            return true
        }
        image.isTemplate = !active
        return image
    }

    @objc private func clicked(_ sender: NSStatusBarButton) {
        panel.toggle(relativeTo: sender)
    }
}
