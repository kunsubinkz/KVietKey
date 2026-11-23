import AppKit
import SwiftUI
import UniformTypeIdentifiers
import VietEngine

enum SettingsTab: Hashable, CaseIterable {
    case basic, typing, apps, macros, advanced, about

    var title: String {
        switch self {
        case .basic: return tr("Cơ bản", "General")
        case .typing: return tr("Gõ tiếng Việt", "Typing")
        case .apps: return tr("Ứng dụng", "Apps")
        case .macros: return tr("Gõ tắt", "Macros")
        case .advanced: return tr("Nâng cao", "Advanced")
        case .about: return tr("Giới thiệu", "About")
        }
    }

    var icon: String {
        switch self {
        case .basic: return "slider.horizontal.3"
        case .typing: return "textformat"
        case .apps: return "square.grid.2x2"
        case .macros: return "bolt"
        case .advanced: return "shield"
        case .about: return "info.circle"
        }
    }
}

final class SettingsRouter: ObservableObject {
    @Published var tab: SettingsTab = .basic
}

final class SettingsWindowController {
    private var window: NSWindow?
    private let router = SettingsRouter()
    private let prefs: Preferences
    private let controller: InputController

    init(prefs: Preferences, controller: InputController) {
        self.prefs = prefs
        self.controller = controller
    }

    func show(tab: SettingsTab) {
        router.tab = tab
        if window == nil {
            let view = SettingsView(prefs: prefs, router: router, controller: controller)
            let w = NSWindow(contentViewController: NSHostingController(rootView: view))
            w.styleMask = [.titled, .closable, .miniaturizable, .fullSizeContentView]
            w.titleVisibility = .hidden
            w.titlebarAppearsTransparent = true
            w.isMovableByWindowBackground = true
            w.isReleasedWhenClosed = false
            w.setContentSize(NSSize(width: 960, height: 680))
            w.center()
            window = w
        }
        NSApp.activate(ignoringOtherApps: true)
        window?.makeKeyAndOrderFront(nil)
    }
}

struct SettingsView: View {
    @ObservedObject var prefs: Preferences
    @ObservedObject var router: SettingsRouter
    @ObservedObject var controller: InputController
    /// Tắt cuộn khi xuất ảnh (ImageRenderer không vẽ được ScrollView).
    var scrollable = true

    var body: some View {
        HStack(spacing: 0) {
            sidebar
            ZStack(alignment: .topLeading) {
                KV.bg
                if scrollable {
                    ScrollView { content }
                } else {
                    content
                }
            }
        }
        .frame(width: 960, height: 680)
        .ignoresSafeArea()
        .environment(\.colorScheme, .light)
    }

    private var content: some View {
        page.padding(32).frame(maxWidth: .infinity, alignment: .topLeading)
    }

    @ViewBuilder private var page: some View {
        switch router.tab {
        case .basic: BasicPage(prefs: prefs)
        case .typing: TypingPage(prefs: prefs)
        case .apps: AppsPage(prefs: prefs)
        case .macros: MacroPage(prefs: prefs)
        case .advanced: AdvancedPage(prefs: prefs)
        case .about: AboutPage()
        }
    }

    private var sidebar: some View {
        VStack(alignment: .leading, spacing: 20) {
            Spacer().frame(height: 12)
            HStack(spacing: 10) {
                KLogo(size: 36)
                Text("KVietKey").font(.kv(17, .bold)).foregroundStyle(Color.white)
            }
            .padding(.horizontal, 8)
            VStack(spacing: 4) {
                ForEach(SettingsTab.allCases, id: \.self) { tab in
                    let active = router.tab == tab
                    Button { router.tab = tab } label: {
                        HStack(spacing: 12) {
                            Image(systemName: tab.icon).font(.system(size: 15, weight: .medium)).frame(width: 20)
                            Text(tab.title).font(.kv(14, .semibold))
                            Spacer()
                        }
                        .foregroundStyle(active ? KV.blue : KV.navyText)
                        .padding(.horizontal, 12)
                        .frame(height: 44)
                        .background(RoundedRectangle(cornerRadius: 12).fill(active ? Color.white : Color.clear))
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                }
            }
            Spacer()
            VStack(alignment: .leading, spacing: 2) {
                Text("\(tr("Phiên bản", "Version")) \(AboutPage.version)").font(.kv(12, .semibold)).foregroundStyle(Color.white)
                Text(AboutPage.architecture).font(.kv(12)).foregroundStyle(KV.navyText)
            }
            .padding(12)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(RoundedRectangle(cornerRadius: 14).fill(Color.white.opacity(0.08)))
        }
        .padding(.horizontal, 16).padding(.vertical, 20)
        .frame(width: 240)
        .frame(maxHeight: .infinity)
        .background(KV.navy)
    }
}

private struct PageScaffold<Trailing: View, Content: View>: View {
    let title: String
    let subtitle: String
    @ViewBuilder var trailing: Trailing
    @ViewBuilder var content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(alignment: .top, spacing: 16) {
                VStack(alignment: .leading, spacing: 4) {
                    Text(title).font(.kv(26, .bold)).foregroundStyle(KV.ink)
                    Text(subtitle).font(.kv(14)).foregroundStyle(KV.muted)
                }
                Spacer()
                trailing
            }
            content
        }
    }
}

extension PageScaffold where Trailing == EmptyView {
    init(title: String, subtitle: String, @ViewBuilder content: () -> Content) {
        self.init(title: title, subtitle: subtitle, trailing: { EmptyView() }, content: content)
    }
}

private struct CardTitle: View {
    let text: String
    init(_ text: String) { self.text = text }
    var body: some View { Text(text).font(.kv(14, .semibold)).foregroundStyle(KV.ink) }
}

private struct Hint: View {
    let text: String
    init(_ text: String) { self.text = text }
    var body: some View {
        Text(text).font(.kv(12)).foregroundStyle(KV.muted).fixedSize(horizontal: false, vertical: true)
    }
}

// MARK: - Cơ bản

private struct BasicPage: View {
    @ObservedObject var prefs: Preferences

    var body: some View {
        PageScaffold(title: tr("Cơ bản", "General"),
                     subtitle: tr("Chọn cách gõ và bảng mã bạn quen dùng.", "Choose the typing style and charset you prefer.")) {
            KCard {
                CardTitle(tr("Kiểu gõ", "Input method"))
                MethodCards(selection: $prefs.settings.method)
            }
            HStack(alignment: .top, spacing: 16) {
                KCard(spacing: 10, fillHeight: true) {
                    CardTitle(tr("Bảng mã", "Charset"))
                    KDropdown(options: Charset.options, selection: $prefs.settings.charset)
                    Hint(tr("Còn có: Unicode tổ hợp, TCVN3, VNI Windows, VISCII, CP1258.",
                            "Also available: composite Unicode, TCVN3, VNI Windows, VISCII, CP1258."))
                }
                KCard(spacing: 10, fillHeight: true) {
                    CardTitle(tr("Phím tắt chuyển Việt ⇄ Anh", "Toggle shortcut Vietnamese ⇄ English"))
                    HotkeyRecorder(hotkey: $prefs.settings.hotkey)
                    Hint(tr("Bấm “Ghi phím mới” rồi nhấn tổ hợp bạn muốn.", "Click “Record new shortcut” and press the combination you want."))
                }
            }
            .fixedSize(horizontal: false, vertical: true)
            KCard(padding: EdgeInsets(top: 6, leading: 20, bottom: 6, trailing: 20), spacing: 0) {
                KToggleRow(title: tr("Khởi động cùng máy", "Launch at login"), isOn: Binding(
                    get: { prefs.settings.launchAtLogin },
                    set: { prefs.settings.launchAtLogin = $0; LaunchAtLogin.set($0) }))
                KToggleRow(title: tr("Phát âm thanh khi chuyển chế độ", "Play a sound when switching"), isOn: $prefs.settings.soundOnToggle)
                KToggleRow(title: tr("Hiện biểu tượng V / E trên thanh menu", "Show V / E icon in the menu bar"),
                           isOn: $prefs.settings.showMenuBarIcon, divider: false)
            }
        }
    }
}

struct HotkeyRecorder: View {
    @Binding var hotkey: Hotkey
    @State private var recording = false
    @State private var monitor: Any?
    @State private var maxModifiers: UInt64 = 0

    var body: some View {
        HStack(spacing: 8) {
            if recording {
                Text(tr("Nhấn tổ hợp phím… (Esc để hủy)", "Press shortcut… (Esc to cancel)"))
                    .font(.kv(13)).foregroundStyle(KV.text2).frame(height: 36)
            } else {
                KeyCaps(hotkey: hotkey)
            }
            Spacer()
            Button { recording ? stop() : start() } label: {
                Text(recording ? tr("Hủy", "Cancel") : tr("Ghi phím mới", "Record new shortcut"))
                    .font(.kv(13, .semibold)).foregroundStyle(KV.blue).lineLimit(1).fixedSize()
                    .padding(.horizontal, 14).frame(height: 36)
                    .background(RoundedRectangle(cornerRadius: 10).fill(KV.soft2))
            }
            .buttonStyle(.plain)
        }
        .frame(height: 44)
        .onDisappear { stop() }
    }

    private func start() {
        recording = true
        maxModifiers = 0
        monitor = NSEvent.addLocalMonitorForEvents(matching: [.keyDown, .flagsChanged]) { event in
            let mods = UInt64(event.modifierFlags.rawValue) & Hotkey.relevantMask
            if event.type == .keyDown {
                if event.keyCode == UInt16(KeyCode.escape) { stop(); return nil }
                if mods != 0 {
                    hotkey = Hotkey(modifiers: mods, keyCode: Int(event.keyCode))
                    stop()
                }
                return nil
            }
            if mods == 0 {
                if maxModifiers.nonzeroBitCount >= 1 {
                    hotkey = Hotkey(modifiers: maxModifiers, keyCode: nil)
                    stop()
                }
            } else if mods.nonzeroBitCount >= maxModifiers.nonzeroBitCount {
                maxModifiers = mods
            }
            return nil
        }
    }

    private func stop() {
        recording = false
        if let monitor { NSEvent.removeMonitor(monitor) }
        monitor = nil
    }
}

// MARK: - Gõ tiếng Việt

private struct TypingPage: View {
    @ObservedObject var prefs: Preferences

    var body: some View {
        PageScaffold(title: tr("Gõ tiếng Việt", "Typing"),
                     subtitle: tr("Tinh chỉnh cách đặt dấu và kiểm tra chính tả.", "Fine-tune tone placement and spell checking.")) {
            KCard(padding: EdgeInsets(top: 6, leading: 20, bottom: 6, trailing: 20), spacing: 0) {
                KToggleRow(title: tr("Đặt dấu kiểu mới (hoà, thuý)", "Modern tone placement (hoà, thuý)"), isOn: $prefs.settings.modernTone)
                KToggleRow(title: tr("Bỏ dấu tự do", "Free marking"),
                           subtitle: tr("Gõ dấu ở bất kỳ vị trí nào trong từ (tieengs, tungw)", "Apply marks anywhere in the word (tieengs, tungw)"),
                           isOn: $prefs.settings.freeMarking, divider: false)
            }
            KCard(padding: EdgeInsets(top: 6, leading: 20, bottom: 6, trailing: 20), spacing: 0) {
                KToggleRow(title: tr("Kiểm tra chính tả", "Spell check"),
                           subtitle: tr("Không biến đổi từ ngoại ngữ như class, show", "Don't transform foreign words like class, show"),
                           isOn: $prefs.settings.spellCheck)
                KToggleRow(title: tr("Tự khôi phục từ tiếng Anh", "Auto-restore English words"),
                           subtitle: tr("Trả lại đúng phím đã gõ khi từ không hợp lệ (windows, text)", "Restore typed keys for invalid words (windows, text)"),
                           isOn: $prefs.settings.autoRestore)
                KToggleRow(title: tr("Cho phép phụ âm z, f, w, j", "Allow consonants z, f, w, j"), isOn: $prefs.settings.allowZFWJ)
                KToggleRow(title: tr("Phím w đứng một mình thành ư (Telex)", "Standalone w becomes ư (Telex)"),
                           isOn: $prefs.settings.wAsU, divider: false)
            }
            KCard(padding: EdgeInsets(top: 6, leading: 20, bottom: 6, trailing: 20), spacing: 0) {
                KToggleRow(title: tr("Viết hoa chữ đầu câu", "Capitalize first letter of sentences"), isOn: $prefs.settings.autoCapitalize)
                KToggleRow(title: tr("Bật gõ tắt", "Enable macros"), isOn: $prefs.settings.macroEnabled, divider: false)
            }
        }
    }
}

// MARK: - Ứng dụng

private struct AppsPage: View {
    @ObservedObject var prefs: Preferences
    @State private var search = ""

    private var rows: [(id: String, name: String, rule: AppRule)] {
        prefs.settings.appRules
            .map { (id: $0.key, name: AppInfo.name(for: $0.key), rule: $0.value) }
            .filter { search.isEmpty || $0.name.localizedCaseInsensitiveContains(search) || $0.id.localizedCaseInsensitiveContains(search) }
            .sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
    }

    var body: some View {
        PageScaffold(title: tr("Ứng dụng", "Apps"),
                     subtitle: tr("Mỗi ứng dụng một chế độ riêng, tự áp dụng khi bạn chuyển cửa sổ.",
                                  "A separate mode per app, applied automatically when you switch windows.")) {
            HStack(spacing: 12) {
                KSearchField(placeholder: tr("Tìm ứng dụng hoặc bundle ID", "Search apps or bundle ID"), text: $search)
                Button { addApp() } label: {
                    HStack(spacing: 8) { Image(systemName: "plus").font(.system(size: 13, weight: .bold)); Text(tr("Thêm ứng dụng", "Add app")) }
                }
                .buttonStyle(KPrimaryButton())
            }
            HStack(spacing: 12) {
                optionCard(tr("Nhớ trạng thái cuối theo từng ứng dụng", "Remember last state per app"), $prefs.settings.rememberAppState)
                optionCard(tr("Tự tắt khi ứng dụng toàn màn hình", "Pause for full-screen apps"), $prefs.settings.disableInFullscreen)
            }
            appTable
        }
    }

    private func optionCard(_ title: String, _ binding: Binding<Bool>) -> some View {
        HStack(spacing: 8) {
            Text(title).font(.kv(13, .medium)).foregroundStyle(KV.ink).fixedSize(horizontal: false, vertical: true)
            Spacer(minLength: 4)
            KSwitch(isOn: binding, label: title)
        }
        .padding(.horizontal, 16)
        .frame(maxWidth: .infinity, minHeight: 52)
        .background(RoundedRectangle(cornerRadius: 14).fill(Color.white))
        .overlay(RoundedRectangle(cornerRadius: 14).stroke(KV.border, lineWidth: 1))
    }

    private var appTable: some View {
        KCard(padding: EdgeInsets(top: 0, leading: 20, bottom: 0, trailing: 20), spacing: 0) {
            HStack {
                Text(tr("Ứng dụng", "App")).frame(maxWidth: .infinity, alignment: .leading)
                Text(tr("Chế độ", "Mode")).frame(width: 190, alignment: .leading)
                Text(tr("Bảng mã", "Charset")).frame(width: 110, alignment: .leading)
                Spacer().frame(width: 28)
            }
            .font(.kv(12, .semibold)).foregroundStyle(KV.muted)
            .frame(height: 38)
            Rectangle().fill(KV.soft).frame(height: 1)
            if rows.isEmpty {
                Text(tr("Chưa có ứng dụng nào. Bấm “Thêm ứng dụng” để đặt chế độ riêng.", "No apps yet. Click “Add app” to set a dedicated mode."))
                    .font(.kv(13)).foregroundStyle(KV.muted)
                    .frame(maxWidth: .infinity, minHeight: 80)
            }
            ForEach(Array(rows.enumerated()), id: \.element.id) { index, row in
                AppRow(prefs: prefs, id: row.id, name: row.name, rule: row.rule, tileIndex: index)
                if index < rows.count - 1 { Rectangle().fill(KV.soft).frame(height: 1) }
            }
        }
    }

    private func addApp() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.application]
        panel.directoryURL = URL(fileURLWithPath: "/Applications")
        guard panel.runModal() == .OK, let url = panel.url, let id = Bundle(url: url)?.bundleIdentifier else { return }
        prefs.settings.appRules[id] = prefs.settings.appRules[id] ?? AppRule(mode: .english)
    }
}

enum AppInfo {
    static func name(for bundleID: String) -> String {
        if let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: bundleID) {
            return FileManager.default.displayName(atPath: url.path).replacingOccurrences(of: ".app", with: "")
        }
        return bundleID
    }
}

private struct AppRow: View {
    @ObservedObject var prefs: Preferences
    let id: String
    let name: String
    let rule: AppRule
    let tileIndex: Int

    private var mode: AppMode { rule.mode == .auto ? .vietnamese : rule.mode }

    var body: some View {
        HStack(spacing: 0) {
            HStack(spacing: 12) {
                let tile = KV.tiles[tileIndex % KV.tiles.count]
                Text(String(name.prefix(1)).uppercased())
                    .font(.kv(15, .bold)).foregroundStyle(tile.fg)
                    .frame(width: 34, height: 34)
                    .background(RoundedRectangle(cornerRadius: 10).fill(tile.bg))
                VStack(alignment: .leading, spacing: 1) {
                    Text(name).font(.kv(14, .semibold)).foregroundStyle(KV.ink).lineLimit(1)
                    Text(id).font(.kv(12)).foregroundStyle(KV.muted).lineLimit(1)
                }
            }
            .frame(maxWidth: .infinity, alignment: .leading)

            HStack(spacing: 3) {
                modeButton(tr("Việt", "VI"), .vietnamese, selectedBg: KV.blue, selectedFg: .white)
                modeButton(tr("Anh", "EN"), .english, selectedBg: .white, selectedFg: KV.navy, shadow: true)
                modeButton(tr("Tắt", "Off"), .off, selectedBg: KV.navy, selectedFg: .white)
            }
            .padding(3)
            .frame(width: 190)
            .background(RoundedRectangle(cornerRadius: 10).fill(KV.soft))

            Group {
                if mode == .off {
                    Text("—").font(.kv(13)).foregroundStyle(KV.text2)
                } else {
                    Menu {
                        Button(tr("Mặc định", "Default")) { update { $0.charset = nil } }
                        ForEach(Charset.allCases, id: \.self) { c in Button(c.displayName) { update { $0.charset = c } } }
                    } label: {
                        HStack(spacing: 4) {
                            Text(rule.charset.map { $0 == .unicode ? "Unicode" : $0.displayName } ?? tr("Mặc định", "Default"))
                                .font(.kv(13)).foregroundStyle(KV.text2).lineLimit(1)
                            Image(systemName: "chevron.down").font(.system(size: 9, weight: .semibold)).foregroundStyle(KV.muted)
                        }
                    }
                    .menuStyle(.borderlessButton)
                    .menuIndicator(.hidden)
                }
            }
            .frame(width: 110, alignment: .leading)
            .padding(.leading, 4)

            Button { prefs.settings.appRules.removeValue(forKey: id) } label: {
                Image(systemName: "trash").font(.system(size: 13)).foregroundStyle(KV.muted).frame(width: 28, height: 28)
            }
            .buttonStyle(.plain)
            .help(tr("Xóa khỏi danh sách", "Remove from list"))
        }
        .frame(height: 56)
    }

    private func modeButton(_ title: String, _ value: AppMode, selectedBg: Color, selectedFg: Color, shadow: Bool = false) -> some View {
        let selected = mode == value
        return Button { update { $0.mode = value } } label: {
            Text(title).font(.kv(12, .semibold))
                .foregroundStyle(selected ? selectedFg : KV.text2)
                .frame(maxWidth: .infinity, minHeight: 28)
                .background(RoundedRectangle(cornerRadius: 8).fill(selected ? selectedBg : Color.clear)
                    .shadow(color: selected && shadow ? KV.ink.opacity(0.2) : .clear, radius: 2, y: 1))
                .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }

    private func update(_ change: (inout AppRule) -> Void) {
        var r = prefs.settings.appRules[id] ?? rule
        change(&r)
        prefs.settings.appRules[id] = r
    }
}

// MARK: - Gõ tắt

private struct MacroEdit: Identifiable {
    let id = UUID()
    var originalKey: String?
    var key: String
    var value: String
}

private struct MacroPage: View {
    @ObservedObject var prefs: Preferences
    @State private var search = ""
    @State private var editing: MacroEdit?

    private var rows: [(key: String, value: String)] {
        prefs.macros.entries
            .filter { search.isEmpty || $0.key.localizedCaseInsensitiveContains(search) || $0.value.localizedCaseInsensitiveContains(search) }
            .map { (key: $0.key, value: $0.value) }
            .sorted { $0.key.localizedCaseInsensitiveCompare($1.key) == .orderedAscending }
    }

    var body: some View {
        PageScaffold(title: tr("Gõ tắt", "Macros"),
                     subtitle: tr("Gõ từ viết tắt rồi nhấn cách để mở rộng thành cả cụm.", "Type an abbreviation then press space to expand it.")) {
            HStack(spacing: 8) {
                Button(tr("Nhập .txt", "Import .txt")) { importFile() }.buttonStyle(KOutlineButton())
                Button(tr("Xuất .txt", "Export .txt")) { exportFile() }.buttonStyle(KOutlineButton())
            }
        } content: {
            HStack(spacing: 12) {
                KSearchField(placeholder: tr("Tìm trong \(prefs.macros.entries.count) mục gõ tắt", "Search \(prefs.macros.entries.count) macros"), text: $search)
                Button { editing = MacroEdit(originalKey: nil, key: "", value: "") } label: {
                    HStack(spacing: 8) { Image(systemName: "plus").font(.system(size: 13, weight: .bold)); Text(tr("Thêm mục", "Add")) }
                }
                .buttonStyle(KPrimaryButton())
            }
            KCard(padding: EdgeInsets(top: 0, leading: 20, bottom: 0, trailing: 20), spacing: 0) {
                HStack {
                    Text(tr("Viết tắt", "Abbreviation")).frame(width: 130, alignment: .leading)
                    Text(tr("Mở rộng thành", "Expands to")).frame(maxWidth: .infinity, alignment: .leading)
                    Spacer().frame(width: 76)
                }
                .font(.kv(12, .semibold)).foregroundStyle(KV.muted)
                .frame(height: 38)
                Rectangle().fill(KV.soft).frame(height: 1)
                if rows.isEmpty {
                    Text(tr("Chưa có mục gõ tắt nào.", "No macros yet."))
                        .font(.kv(13)).foregroundStyle(KV.muted)
                        .frame(maxWidth: .infinity, minHeight: 80)
                }
                ForEach(Array(rows.enumerated()), id: \.element.key) { index, row in
                    HStack {
                        Text(row.key).font(.kv(13, .bold)).foregroundStyle(KV.blue)
                            .padding(.horizontal, 10).padding(.vertical, 4)
                            .background(RoundedRectangle(cornerRadius: 8).fill(KV.soft2))
                            .frame(width: 130, alignment: .leading)
                        Text(row.value).font(.kv(14, .medium)).foregroundStyle(KV.ink)
                            .frame(maxWidth: .infinity, alignment: .leading).lineLimit(1)
                        HStack(spacing: 4) {
                            iconButton("pencil", tr("Sửa \(row.key)", "Edit \(row.key)")) {
                                editing = MacroEdit(originalKey: row.key, key: row.key, value: row.value)
                            }
                            iconButton("trash", tr("Xóa \(row.key)", "Delete \(row.key)")) {
                                prefs.macros.entries.removeValue(forKey: row.key)
                            }
                        }
                        .frame(width: 76, alignment: .trailing)
                    }
                    .frame(height: 50)
                    if index < rows.count - 1 { Rectangle().fill(KV.soft).frame(height: 1) }
                }
            }
            HStack(spacing: 16) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(tr("Giữ kiểu hoa / thường", "Keep letter case")).font(.kv(14, .semibold)).foregroundStyle(KV.ink)
                    Text("vn → Việt Nam · VN → VIỆT NAM").font(.kv(12)).foregroundStyle(KV.text2)
                }
                Spacer()
                KSwitch(isOn: $prefs.settings.macroKeepCase, label: tr("Giữ kiểu hoa / thường", "Keep letter case"))
            }
            .padding(.horizontal, 16).padding(.vertical, 12)
            .background(RoundedRectangle(cornerRadius: 14).fill(KV.banner))
        }
        .sheet(item: $editing) { edit in
            MacroEditSheet(edit: edit) { saved in
                if let old = saved.originalKey, old != saved.key { prefs.macros.entries.removeValue(forKey: old) }
                prefs.macros.entries[saved.key] = saved.value
                editing = nil
            } cancel: { editing = nil }
        }
    }

    private func iconButton(_ name: String, _ label: String, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            Image(systemName: name).font(.system(size: 14)).foregroundStyle(KV.text2).frame(width: 34, height: 34)
        }
        .buttonStyle(.plain)
        .help(label)
        .accessibilityLabel(label)
    }

    private func importFile() {
        let panel = NSOpenPanel()
        panel.allowedContentTypes = [.plainText]
        guard panel.runModal() == .OK, let url = panel.url, let text = try? String(contentsOf: url, encoding: .utf8) else { return }
        var merged = prefs.macros
        merged.merge(MacroTable.parse(text: text))
        prefs.macros = merged
    }

    private func exportFile() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [.plainText]
        panel.nameFieldStringValue = "kvietkey-macros.txt"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        try? prefs.macros.exportText().write(to: url, atomically: true, encoding: .utf8)
    }
}

private struct MacroEditSheet: View {
    @State var edit: MacroEdit
    let save: (MacroEdit) -> Void
    let cancel: () -> Void

    private var valid: Bool { !edit.key.trimmingCharacters(in: .whitespaces).isEmpty && !edit.value.isEmpty }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text(edit.originalKey == nil ? tr("Thêm gõ tắt", "Add macro") : tr("Sửa gõ tắt", "Edit macro"))
                .font(.kv(18, .bold)).foregroundStyle(KV.ink)
            VStack(alignment: .leading, spacing: 6) {
                Text(tr("Viết tắt", "Abbreviation")).font(.kv(12, .semibold)).foregroundStyle(KV.muted)
                field($edit.key, placeholder: "vn")
            }
            VStack(alignment: .leading, spacing: 6) {
                Text(tr("Mở rộng thành", "Expands to")).font(.kv(12, .semibold)).foregroundStyle(KV.muted)
                field($edit.value, placeholder: "Việt Nam")
            }
            HStack {
                Spacer()
                Button(tr("Hủy", "Cancel"), action: cancel).buttonStyle(KTextButton())
                Button(tr("Lưu", "Save")) {
                    var e = edit
                    e.key = e.key.trimmingCharacters(in: .whitespaces)
                    save(e)
                }
                .buttonStyle(KPrimaryButton())
                .disabled(!valid)
                .opacity(valid ? 1 : 0.5)
                .keyboardShortcut(.defaultAction)
            }
        }
        .padding(24)
        .frame(width: 420)
        .background(Color.white)
        .environment(\.colorScheme, .light)
    }

    private func field(_ text: Binding<String>, placeholder: String) -> some View {
        TextField(placeholder, text: text)
            .textFieldStyle(.plain)
            .font(.kv(14)).foregroundStyle(KV.ink)
            .padding(.horizontal, 14).frame(height: 44)
            .background(RoundedRectangle(cornerRadius: 12).fill(KV.bg))
            .overlay(RoundedRectangle(cornerRadius: 12).stroke(KV.border2, lineWidth: 1))
    }
}

// MARK: - Nâng cao

private struct AdvancedPage: View {
    @ObservedObject var prefs: Preferences
    @State private var conflicts: [String] = []

    var body: some View {
        PageScaffold(title: tr("Nâng cao", "Advanced"),
                     subtitle: tr("Tương thích theo ứng dụng và công cụ chẩn đoán.", "Per-app compatibility and diagnostics.")) {
            KCard(padding: EdgeInsets(top: 6, leading: 20, bottom: 14, trailing: 20), spacing: 0) {
                KToggleRow(title: tr("Sửa lỗi gợi ý của trình duyệt", "Fix browser autocomplete"),
                           subtitle: tr("Thanh địa chỉ và ô tìm kiếm của Chrome, Safari, Edge…", "Address bars and search boxes in Chrome, Safari, Edge…"),
                           isOn: $prefs.settings.browserAutocompleteFix)
                VStack(alignment: .leading, spacing: 8) {
                    Text(tr("Độ trễ cho ứng dụng Electron (Slack, Discord, VS Code…): \(prefs.settings.electronDelayMicros) µs",
                            "Delay for Electron apps (Slack, Discord, VS Code…): \(prefs.settings.electronDelayMicros) µs"))
                        .font(.kv(14, .medium)).foregroundStyle(KV.ink)
                    Slider(value: Binding(get: { Double(prefs.settings.electronDelayMicros) },
                                          set: { prefs.settings.electronDelayMicros = Int($0) }), in: 0...10000, step: 250)
                        .tint(KV.blue)
                }
                .padding(.top, 12)
            }
            KCard(padding: EdgeInsets(top: 6, leading: 20, bottom: 14, trailing: 20), spacing: 0) {
                KToggleRow(title: tr("Ghi log debug", "Debug log"),
                           subtitle: tr("Chỉ ghi sự kiện, không bao giờ ghi nội dung gõ", "Events only, never typed content"),
                           isOn: $prefs.settings.debugLog)
                if conflicts.isEmpty {
                    Hint(tr("Không phát hiện bộ gõ khác đang chạy.", "No other input method apps detected.")).padding(.top, 12)
                } else {
                    Text(tr("Đang chạy bộ gõ khác: \(conflicts.joined(separator: ", ")). Hãy thoát chúng để tránh xung đột.",
                            "Other input apps running: \(conflicts.joined(separator: ", ")). Quit them to avoid conflicts."))
                        .font(.kv(13, .medium)).foregroundStyle(Color.orange).padding(.top, 12)
                }
                Hint(tr("Nếu dùng bàn phím tiếng Việt có sẵn của macOS, hãy xóa nó khỏi Input Sources để tránh gõ trùng.",
                        "If you use Apple's built-in Vietnamese keyboard, remove it from Input Sources to avoid double processing."))
                    .padding(.top, 6)
            }
            KCard(spacing: 10) {
                CardTitle(tr("Ngôn ngữ giao diện", "Interface language"))
                KSegmented(options: [(AppLanguage.auto, tr("Tự động", "Automatic")), (.vi, "Tiếng Việt"), (.en, "English")],
                           selection: $prefs.settings.language)
            }
            HStack {
                Button(tr("Khôi phục cài đặt mặc định", "Reset to defaults")) {
                    var fresh = Settings()
                    fresh.onboardingDone = true
                    fresh.appRules = prefs.settings.appRules
                    prefs.settings = fresh
                }
                .buttonStyle(KOutlineButton())
                Spacer()
            }
        }
        .onAppear { conflicts = AdvancedPage.runningConflicts() }
    }

    static func runningConflicts() -> [String] {
        let names = NSWorkspace.shared.runningApplications.compactMap { $0.localizedName }
        return AppCompat.conflictingInputApps.filter { c in names.contains { $0.localizedCaseInsensitiveContains(c) } }
    }
}

// MARK: - Giới thiệu

struct AboutPage: View {
    var body: some View {
        PageScaffold(title: tr("Giới thiệu", "About"),
                     subtitle: tr("Bộ gõ tiếng Việt native cho macOS.", "Native Vietnamese input method for macOS.")) {
            KCard(spacing: 14) {
                HStack(spacing: 14) {
                    KLogo(size: 64)
                    VStack(alignment: .leading, spacing: 2) {
                        Text("KVietKey").font(.kv(22, .bold)).foregroundStyle(KV.ink)
                        Text("\(tr("Phiên bản", "Version")) \(Self.version) · \(Self.architecture)").font(.kv(13)).foregroundStyle(KV.muted)
                    }
                }
                Rectangle().fill(KV.soft).frame(height: 1)
                Text(tr("Riêng tư: KVietKey không lưu, không gửi nội dung bạn gõ và không chứa mã mạng.",
                        "Privacy: KVietKey never stores or sends what you type and contains no network code."))
                    .font(.kv(14)).foregroundStyle(KV.text2).fixedSize(horizontal: false, vertical: true)
                Text(tr("Được tạo bởi kunsubin", "Created by kunsubin"))
                    .font(.kv(14, .semibold)).foregroundStyle(KV.ink)
                Hint(tr("© 2026 kunsubin. Phát hành theo giấy phép MIT.", "© 2026 kunsubin. Released under the MIT license."))
            }
        }
    }

    static var version: String {
        let info = Bundle.main.infoDictionary
        let v = info?["CFBundleShortVersionString"] as? String ?? "dev"
        let b = info?["CFBundleVersion"] as? String ?? "0"
        return b == "0" ? v : "\(v) (\(b))"
    }

    static var architecture: String {
        #if arch(arm64)
        return "Apple Silicon · arm64"
        #else
        return "Intel · x86_64"
        #endif
    }
}
