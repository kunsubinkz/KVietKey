import AppKit
import SwiftUI
import VietEngine

/// Panel bo góc hiện ngay dưới icon menu bar, thay cho NSMenu mặc định.
final class MenuPanel: NSPanel {
    override var canBecomeKey: Bool { true }
}

final class MenuPanelController: NSObject, NSWindowDelegate {
    private var panel: MenuPanel?
    private var clickMonitor: Any?
    private let makeContent: (@escaping () -> Void) -> AnyView

    init(makeContent: @escaping (@escaping () -> Void) -> AnyView) {
        self.makeContent = makeContent
    }

    var isShown: Bool { panel?.isVisible == true }

    func toggle(relativeTo button: NSStatusBarButton) {
        isShown ? close() : show(relativeTo: button)
    }

    func show(relativeTo button: NSStatusBarButton) {
        close()
        let host = NSHostingView(rootView: makeContent { [weak self] in self?.close() })
        host.setFrameSize(host.fittingSize)
        let panel = MenuPanel(contentRect: NSRect(origin: .zero, size: host.frame.size),
                              styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.contentView = host
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = true
        panel.level = .popUpMenu
        panel.collectionBehavior = [.canJoinAllSpaces, .transient]
        panel.delegate = self

        if let window = button.window {
            let rect = window.convertToScreen(button.convert(button.bounds, to: nil))
            let screen = window.screen?.visibleFrame ?? NSScreen.main?.visibleFrame ?? .zero
            let size = panel.frame.size
            let x = min(max(rect.maxX - size.width + 8, screen.minX + 8), screen.maxX - size.width - 8)
            panel.setFrameTopLeftPoint(NSPoint(x: x, y: rect.minY - 6))
        }
        panel.makeKeyAndOrderFront(nil)
        self.panel = panel
        clickMonitor = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDown, .rightMouseDown]) { [weak self] _ in
            self?.close()
        }
    }

    func close() {
        if let clickMonitor { NSEvent.removeMonitor(clickMonitor) }
        clickMonitor = nil
        panel?.delegate = nil
        panel?.orderOut(nil)
        panel = nil
    }

    func windowDidResignKey(_ notification: Notification) { close() }
}

/// Nội dung panel: bật/tắt Việt–Anh, kiểu gõ, bảng mã, tuỳ chọn nhanh và lối tắt.
struct MenuPanelView: View {
    @ObservedObject var prefs: Preferences
    @ObservedObject var controller: InputController
    let openSettings: (SettingsTab) -> Void
    let quit: () -> Void
    let close: () -> Void

    private var vietnameseBinding: Binding<Bool> {
        Binding(get: { controller.vietnamese }, set: { controller.setVietnamese($0) })
    }

    private var statusText: String {
        if controller.secureInput { return tr("Tạm dừng · ô nhập bảo mật", "Paused · secure input") }
        if controller.fullscreenSuspended { return tr("Tạm dừng · toàn màn hình", "Paused · full screen") }
        return controller.vietnamese
            ? tr("Đang bật · ", "On · ") + prefs.settings.method.displayName
            : tr("Đang tắt tiếng Việt", "Vietnamese is off")
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            header
            languageSwitch
            methodSection
            charsetRow
            toggles
            Rectangle().fill(KV.border).frame(height: 1)
            actions
        }
        .padding(16)
        .frame(width: 360)
        .background(RoundedRectangle(cornerRadius: 20).fill(Color.white))
        .padding(10)
        .onExitCommand(perform: close)
    }

    private var header: some View {
        HStack(spacing: 12) {
            KLogo(size: 40)
            VStack(alignment: .leading, spacing: 2) {
                Text("KVietKey").font(.kv(16, .bold)).foregroundStyle(KV.ink)
                Text(statusText).font(.kv(12)).foregroundStyle(KV.muted)
            }
            Spacer()
            Button { close(); openSettings(.basic) } label: {
                Image(systemName: "slider.horizontal.3")
                    .font(.system(size: 15, weight: .medium))
                    .foregroundStyle(KV.blue)
                    .frame(width: 36, height: 36)
                    .background(RoundedRectangle(cornerRadius: 10).fill(KV.soft))
            }
            .buttonStyle(.plain)
            .help(tr("Mở cài đặt", "Open settings"))
        }
    }

    private var languageSwitch: some View {
        VStack(spacing: 8) {
            HStack(spacing: 4) {
                langButton(letter: "V", title: "Tiếng Việt", value: true)
                langButton(letter: "E", title: "English", value: false)
            }
            .padding(4)
            .background(RoundedRectangle(cornerRadius: 18).fill(KV.soft))
            HStack {
                Text(tr("Chuyển nhanh bằng phím tắt", "Quick switch shortcut"))
                Spacer()
                KeyCaps(hotkey: prefs.settings.hotkey, large: false)
            }
            .font(.kv(12)).foregroundStyle(KV.muted)
            .padding(.horizontal, 4)
        }
    }

    private func langButton(letter: String, title: String, value: Bool) -> some View {
        let selected = controller.vietnamese == value
        return Button { vietnameseBinding.wrappedValue = value } label: {
            HStack(spacing: 10) {
                Text(letter).font(.kv(14, .bold))
                    .foregroundStyle(selected ? Color.white : KV.blue)
                    .frame(width: 28, height: 28)
                    .background(RoundedRectangle(cornerRadius: 9).fill(selected ? Color.white.opacity(0.22) : Color.white))
                Text(title).font(.kv(15, .semibold))
            }
            .foregroundStyle(selected ? Color.white : KV.text2)
            .frame(maxWidth: .infinity, minHeight: 52)
            .background(RoundedRectangle(cornerRadius: 14).fill(selected ? KV.blue : Color.clear)
                .shadow(color: selected ? KV.blue.opacity(0.35) : .clear, radius: 6, y: 4))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }

    private var methodSection: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(tr("Kiểu gõ", "Input method")).font(.kv(12, .semibold)).foregroundStyle(KV.muted).padding(.leading, 2)
            KSegmented(options: InputMethod.allCases.map { ($0, $0.title) },
                       selection: $prefs.settings.method, height: 34, fontSize: 13, radius: 12)
        }
    }

    /// Dòng "Bảng mã    Unicode dựng sẵn ⌄" cùng cỡ chữ với các dòng bên dưới; bấm mở danh sách bảng mã.
    /// Mục cuối là chuyển mã văn bản đã copy (ít dùng nên gom vào đây).
    private var charsetRow: some View {
        CharsetPickerRow(title: tr("Bảng mã", "Charset"), value: prefs.settings.charset.displayName) { anchor in
            let target = MenuTarget()
            let menu = NSMenu()
            for c in Charset.allCases {
                let item = target.item(c.displayName) { prefs.settings.charset = c }
                item.state = prefs.settings.charset == c ? .on : .off
                menu.addItem(item)
            }
            menu.addItem(.separator())
            let convert = NSMenuItem(title: tr("Chuyển mã văn bản đã copy", "Convert copied text"), action: nil, keyEquivalent: "")
            let sub = NSMenu()
            for c in ClipboardConverter.conversions {
                sub.addItem(target.item(c.title) { if !ClipboardConverter.convert(c) { NSSound.beep() }; close() })
            }
            convert.submenu = sub
            menu.addItem(convert)
            withExtendedLifetime(target) { menu.popUp(positioning: nil, at: anchor, in: nil) }
        }
    }

    private var toggles: some View {
        VStack(spacing: 0) {
            KToggleRow(title: tr("Kiểm tra chính tả", "Spell check"), isOn: $prefs.settings.spellCheck, divider: false, height: 34)
            KToggleRow(title: tr("Tự khôi phục từ tiếng Anh", "Auto-restore English words"), isOn: $prefs.settings.autoRestore, divider: false, height: 34)
            KToggleRow(title: tr("Đặt dấu kiểu mới (hoà, thuý)", "Modern tone placement (hoà, thuý)"), isOn: $prefs.settings.modernTone, divider: false, height: 34)
        }
    }

    private var actions: some View {
        VStack(spacing: 2) {
            MenuActionRow(title: tr("Gõ tắt…", "Macros…"), shortcut: "⌘M") { close(); openSettings(.macros) }
                .keyboardShortcut("m", modifiers: .command)
            MenuActionRow(title: tr("Cài đặt…", "Settings…"), shortcut: "⌘,") { close(); openSettings(.basic) }
                .keyboardShortcut(",", modifiers: .command)
            MenuActionRow(title: tr("Thoát KVietKey", "Quit KVietKey"), shortcut: "⌘Q") { quit() }
                .keyboardShortcut("q", modifiers: .command)
        }
    }
}

private struct MenuActionLabel: View {
    let title: String
    let shortcut: String
    var hovering = false

    var body: some View {
        HStack {
            Text(title).font(.kv(14, .medium)).foregroundStyle(KV.ink)
            Spacer()
            Text(shortcut).font(.kv(12)).foregroundStyle(KV.muted)
        }
        .padding(.horizontal, 10)
        .frame(height: 38)
        .background(RoundedRectangle(cornerRadius: 10).fill(hovering ? KV.soft : Color.clear))
        .contentShape(Rectangle())
    }
}

private struct MenuActionRow: View {
    let title: String
    let shortcut: String
    let action: () -> Void
    @State private var hovering = false

    var body: some View {
        Button(action: action) { MenuActionLabel(title: title, shortcut: shortcut, hovering: hovering) }
            .buttonStyle(.plain)
            .onHover { hovering = $0 }
    }
}

/// Gắn closure vào NSMenuItem.
private final class MenuTarget: NSObject {
    private var actions: [() -> Void] = []

    func item(_ title: String, _ action: @escaping () -> Void) -> NSMenuItem {
        actions.append(action)
        let item = NSMenuItem(title: title, action: #selector(fire(_:)), keyEquivalent: "")
        item.target = self
        item.tag = actions.count - 1
        return item
    }

    @objc private func fire(_ sender: NSMenuItem) { actions[sender.tag]() }
}

private struct CharsetPickerRow: View {
    let title: String
    let value: String
    let open: (NSPoint) -> Void
    @State private var hovering = false

    var body: some View {
        Button {
            let p = NSEvent.mouseLocation
            open(NSPoint(x: p.x, y: p.y))
        } label: {
            HStack(spacing: 6) {
                Text(title).font(.kv(14, .medium)).foregroundStyle(KV.ink)
                Spacer()
                Text(value).font(.kv(14, .semibold)).foregroundStyle(KV.blue)
                Image(systemName: "chevron.up.chevron.down").font(.system(size: 10, weight: .semibold)).foregroundStyle(KV.muted)
            }
            .padding(.horizontal, 10)
            .frame(height: 38)
            .background(RoundedRectangle(cornerRadius: 10).fill(hovering ? KV.soft : KV.bg))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .onHover { hovering = $0 }
    }
}
