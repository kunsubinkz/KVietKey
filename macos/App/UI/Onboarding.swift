import AppKit
import SwiftUI
import VietEngine

final class OnboardingModel: ObservableObject {
    @Published var step: Int
    @Published var trusted = AccessibilityGuard.isTrusted
    let prefs: Preferences
    var onGranted: () -> Void = {}
    var onFinish: () -> Void = {}
    private var timer: Timer?

    init(prefs: Preferences, step: Int) {
        self.prefs = prefs
        self.step = step
        if step == 2 { startPolling() }
    }

    func go(to newStep: Int) {
        step = newStep
        newStep == 2 ? startPolling() : stopPolling()
    }

    func startPolling() {
        stopPolling()
        check()
        guard step == 2 else { return }
        timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in self?.check() }
    }

    func stopPolling() {
        timer?.invalidate()
        timer = nil
    }

    private func check() {
        guard AccessibilityGuard.isTrusted else { return }
        trusted = true
        guard step == 2 else { return }
        stopPolling()
        onGranted()
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.8) { [weak self] in
            if self?.step == 2 { self?.go(to: 3) }
        }
    }
}

/// Cửa sổ chào mừng 3 bước: Chào mừng → Cấp quyền → Kiểu gõ.
final class OnboardingWindowController: NSObject, NSWindowDelegate {
    private var window: NSWindow?
    private var model: OnboardingModel?
    private let prefs: Preferences
    private let onGranted: () -> Void

    init(prefs: Preferences, onGranted: @escaping () -> Void) {
        self.prefs = prefs
        self.onGranted = onGranted
    }

    func show(startingAt step: Int) {
        if let window {
            window.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)
            return
        }
        let model = OnboardingModel(prefs: prefs, step: step)
        model.onGranted = onGranted
        model.onFinish = { [weak self] in self?.finish() }
        self.model = model
        let w = NSWindow(contentViewController: NSHostingController(rootView: OnboardingView(model: model, prefs: prefs)))
        w.styleMask = [.titled, .closable, .fullSizeContentView]
        w.titleVisibility = .hidden
        w.titlebarAppearsTransparent = true
        w.isMovableByWindowBackground = true
        w.isReleasedWhenClosed = false
        w.setContentSize(NSSize(width: 880, height: 580))
        w.delegate = self
        w.center()
        window = w
        NSApp.activate(ignoringOtherApps: true)
        w.makeKeyAndOrderFront(nil)
    }

    private func finish() {
        prefs.settings.onboardingDone = true
        close()
    }

    private func close() {
        model?.stopPolling()
        window?.delegate = nil
        window?.close()
        window = nil
        model = nil
    }

    func windowWillClose(_ notification: Notification) {
        model?.stopPolling()
        window = nil
        model = nil
    }
}

struct OnboardingView: View {
    @ObservedObject var model: OnboardingModel
    @ObservedObject var prefs: Preferences

    var body: some View {
        HStack(spacing: 0) {
            leftPanel
            VStack(alignment: .leading, spacing: 24) {
                stepper
                switch model.step {
                case 1: welcome
                case 2: permission
                default: methodChoice
                }
            }
            .padding(.horizontal, 48).padding(.vertical, 40)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            .background(Color.white)
        }
        .frame(width: 880, height: 580)
        .ignoresSafeArea()
        .environment(\.colorScheme, .light)
    }

    // MARK: Cột trái

    private var leftPanel: some View {
        VStack(alignment: .leading, spacing: 24) {
            HStack(spacing: 12) {
                KLogo(size: 44)
                Text("KVietKey").font(.kv(20, .bold)).foregroundStyle(Color.white)
            }
            Text(tr("Gõ tiếng Việt, nhẹ như không.", "Type Vietnamese, light as air."))
                .font(.kv(30, .bold)).foregroundStyle(Color.white)
                .fixedSize(horizontal: false, vertical: true)
            VStack(alignment: .leading, spacing: 0) {
                Text(tr("Bạn gõ", "You type")).font(.kv(13)).foregroundStyle(KV.navyText)
                Text("tieengs vieetj").font(.kv(16, .medium)).foregroundStyle(Color.white).padding(.top, 2)
                Rectangle().fill(Color.white.opacity(0.18)).frame(height: 1).padding(.vertical, 12)
                Text(tr("Hiện ra", "You get")).font(.kv(13)).foregroundStyle(KV.navyText)
                Text("tiếng việt").font(.kv(26, .bold)).foregroundStyle(KV.cyan).padding(.top, 2)
            }
            .padding(16)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(RoundedRectangle(cornerRadius: 16).fill(Color.white.opacity(0.08)))
            .overlay(RoundedRectangle(cornerRadius: 16).stroke(Color.white.opacity(0.18), lineWidth: 1))
            Spacer()
            VStack(alignment: .leading, spacing: 12) {
                bullet(tr("Chạy gốc trên Apple Silicon", "Native on Apple Silicon"))
                bullet(tr("Không gạch chân khi đang gõ", "No underline while typing"))
                bullet(tr("Không lưu, không gửi nội dung gõ", "Never stores or sends what you type"))
            }
        }
        .padding(.horizontal, 36).padding(.vertical, 40)
        .frame(width: 340)
        .frame(maxHeight: .infinity, alignment: .topLeading)
        .background(KV.navy)
    }

    private func bullet(_ text: String) -> some View {
        HStack(spacing: 10) {
            Image(systemName: "checkmark").font(.system(size: 12, weight: .heavy)).foregroundStyle(KV.cyan)
            Text(text).font(.kv(14)).foregroundStyle(Color(hex: 0xE6EDFF))
        }
    }

    // MARK: Thanh bước

    private var stepper: some View {
        HStack(spacing: 12) {
            stepNode(1, tr("Chào mừng", "Welcome"))
            line(model.step > 1)
            stepNode(2, tr("Cấp quyền", "Permission"))
            line(model.step > 2)
            stepNode(3, tr("Kiểu gõ", "Typing style"))
        }
        .font(.kv(13, .semibold))
    }

    private func line(_ filled: Bool) -> some View {
        RoundedRectangle(cornerRadius: 1).fill(filled ? KV.blue : KV.border2).frame(height: 2)
    }

    private func stepNode(_ n: Int, _ title: String) -> some View {
        let done = model.step > n
        let current = model.step == n
        return HStack(spacing: 8) {
            ZStack {
                Circle().fill(done ? KV.blue : Color.clear)
                Circle().stroke(done || current ? KV.blue : KV.keyBorder, lineWidth: 2)
                if done {
                    Image(systemName: "checkmark").font(.system(size: 11, weight: .heavy)).foregroundStyle(Color.white)
                } else {
                    Text("\(n)").foregroundStyle(current ? KV.blue : KV.muted)
                }
            }
            .frame(width: 26, height: 26)
            Text(title).foregroundStyle(done ? KV.blue : (current ? KV.ink : KV.muted)).lineLimit(1).fixedSize()
        }
    }

    // MARK: Bước 1

    private var welcome: some View {
        VStack(alignment: .leading, spacing: 24) {
            VStack(alignment: .leading, spacing: 10) {
                Text(tr("Chào mừng đến với KVietKey", "Welcome to KVietKey")).font(.kv(26, .bold)).foregroundStyle(KV.ink)
                Text(tr("Bộ gõ tiếng Việt viết mới hoàn toàn, nhẹ và chạy gốc trên Mac Apple Silicon. Chỉ mất vài bước để bắt đầu.",
                        "A brand-new Vietnamese input method, light and native on Apple Silicon Macs. It only takes a few steps to get started."))
                    .font(.kv(15)).foregroundStyle(KV.text2).lineSpacing(4)
                    .fixedSize(horizontal: false, vertical: true)
            }
            VStack(alignment: .leading, spacing: 10) {
                infoRow("keyboard", tr("Telex, VNI, VIQR và nhiều bảng mã", "Telex, VNI, VIQR and many charsets"))
                infoRow("app.badge", tr("Cài đặt riêng cho từng ứng dụng", "Per-app settings"))
                infoRow("lock.shield", tr("Không có mã mạng, không thu thập dữ liệu", "No network code, no data collection"))
            }
            Spacer()
            HStack {
                Button(tr("Bắt đầu", "Get started")) { model.go(to: 2) }
                    .buttonStyle(KPrimaryButton(height: 48)).keyboardShortcut(.defaultAction)
                Spacer()
                Text(tr("Bước 1/3", "Step 1/3")).font(.kv(13)).foregroundStyle(KV.muted)
            }
        }
    }

    private func infoRow(_ icon: String, _ text: String) -> some View {
        HStack(spacing: 12) {
            Image(systemName: icon).font(.system(size: 15, weight: .medium)).foregroundStyle(KV.blue)
                .frame(width: 34, height: 34)
                .background(RoundedRectangle(cornerRadius: 10).fill(KV.soft2))
            Text(text).font(.kv(14, .medium)).foregroundStyle(KV.ink)
        }
    }

    // MARK: Bước 2

    private var permission: some View {
        VStack(alignment: .leading, spacing: 24) {
            VStack(alignment: .leading, spacing: 10) {
                Text(tr("Cho phép KVietKey đọc phím gõ", "Allow KVietKey to read keystrokes")).font(.kv(26, .bold)).foregroundStyle(KV.ink)
                (Text(tr("macOS cần bạn bật quyền ", "macOS needs you to enable the ")) + Text(tr("Trợ năng", "Accessibility")).bold().foregroundColor(KV.ink)
                    + Text(tr(" để bộ gõ biến phím thành chữ có dấu. Quyền này chỉ dùng để xử lý từng từ trong bộ nhớ, không có kết nối mạng nào gửi nội dung ra ngoài.",
                              " permission so the input method can turn keys into accented text. It is only used to process each word in memory; no network connection sends your content anywhere.")))
                    .font(.kv(15)).foregroundStyle(KV.text2).lineSpacing(4)
                    .fixedSize(horizontal: false, vertical: true)
            }
            VStack(alignment: .leading, spacing: 12) {
                Text(tr("Cài đặt Hệ thống › Quyền riêng tư & Bảo mật › Trợ năng", "System Settings › Privacy & Security › Accessibility"))
                    .font(.kv(12, .medium)).foregroundStyle(KV.muted)
                HStack(spacing: 12) {
                    KLogo(size: 32)
                    Text("KVietKey").font(.kv(14, .semibold)).foregroundStyle(KV.ink)
                    Spacer()
                    KSwitch(isOn: .constant(model.trusted), label: "KVietKey").allowsHitTesting(false)
                }
                .padding(.horizontal, 12).padding(.vertical, 10)
                .background(RoundedRectangle(cornerRadius: 12).fill(Color.white))
                .overlay(RoundedRectangle(cornerRadius: 12).stroke(KV.border, lineWidth: 1))
            }
            .padding(16)
            .background(RoundedRectangle(cornerRadius: 16).fill(KV.bg))
            .overlay(RoundedRectangle(cornerRadius: 16).stroke(KV.border2, lineWidth: 1))

            HStack(spacing: 10) {
                Circle().fill(model.trusted ? Color.green : KV.cyanDot).frame(width: 10, height: 10)
                    .overlay(Circle().stroke(model.trusted ? Color.green.opacity(0.25) : KV.cyanSoft, lineWidth: 4))
                Text(model.trusted
                     ? tr("Đã cấp quyền. Đang chuyển sang bước tiếp theo…", "Permission granted. Moving on…")
                     : tr("Đang chờ bạn cấp quyền… KVietKey tự kiểm tra mỗi giây.", "Waiting for permission… KVietKey checks every second."))
                    .font(.kv(13)).foregroundStyle(KV.text2)
            }
            Text(tr("Lưu ý: bản tự ký (ad-hoc) cần cấp lại quyền sau mỗi lần build mới.", "Note: ad-hoc signed builds need the permission re-granted after each rebuild."))
                .font(.kv(12)).foregroundStyle(KV.muted)
            Spacer()
            HStack(spacing: 12) {
                Button(tr("Mở Cài đặt Hệ thống", "Open System Settings")) {
                    AccessibilityGuard.requestPrompt()
                    AccessibilityGuard.openSystemSettings()
                }
                .buttonStyle(KPrimaryButton(height: 48)).keyboardShortcut(.defaultAction)
                Button(tr("Để sau", "Later")) { model.go(to: 3) }.buttonStyle(KTextButton())
                Spacer()
                Text(tr("Bước 2/3", "Step 2/3")).font(.kv(13)).foregroundStyle(KV.muted)
            }
        }
    }

    // MARK: Bước 3

    private var methodChoice: some View {
        VStack(alignment: .leading, spacing: 20) {
            VStack(alignment: .leading, spacing: 10) {
                Text(tr("Chọn kiểu gõ quen thuộc", "Choose your typing style")).font(.kv(26, .bold)).foregroundStyle(KV.ink)
                Text(tr("Bạn có thể đổi lại bất cứ lúc nào trong Cài đặt hoặc ngay trên thanh menu.", "You can change it any time in Settings or right from the menu bar."))
                    .font(.kv(15)).foregroundStyle(KV.text2).fixedSize(horizontal: false, vertical: true)
            }
            VStack(spacing: 10) {
                ForEach(InputMethod.allCases, id: \.self) { m in
                    let selected = prefs.settings.method == m
                    Button { prefs.settings.method = m } label: {
                        HStack {
                            VStack(alignment: .leading, spacing: 2) {
                                Text(m.title).font(.kv(16, .bold)).foregroundStyle(selected ? KV.blue : KV.ink)
                                Text(m.example).font(.kv(13)).foregroundStyle(KV.muted)
                            }
                            Spacer()
                            if selected {
                                Image(systemName: "checkmark").font(.system(size: 10, weight: .heavy)).foregroundStyle(Color.white)
                                    .frame(width: 20, height: 20).background(Circle().fill(KV.blue))
                            }
                        }
                        .padding(.horizontal, 16).padding(.vertical, 10)
                        .background(RoundedRectangle(cornerRadius: 14).fill(selected ? KV.soft : Color.white))
                        .overlay(RoundedRectangle(cornerRadius: 14).stroke(selected ? KV.blue : KV.border, lineWidth: 2))
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                }
            }
            HStack(spacing: 8) {
                Text(tr("Phím tắt chuyển Việt ⇄ Anh", "Toggle Vietnamese ⇄ English")).font(.kv(13)).foregroundStyle(KV.muted)
                KeyCaps(hotkey: prefs.settings.hotkey, large: false)
            }
            Spacer()
            HStack(spacing: 12) {
                Button(tr("Hoàn tất", "Finish")) { model.onFinish() }
                    .buttonStyle(KPrimaryButton(height: 48)).keyboardShortcut(.defaultAction)
                Button(tr("Quay lại", "Back")) { model.go(to: 2) }.buttonStyle(KTextButton())
                Spacer()
                Text(tr("Bước 3/3", "Step 3/3")).font(.kv(13)).foregroundStyle(KV.muted)
            }
        }
    }
}
