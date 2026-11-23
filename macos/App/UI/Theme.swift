import AppKit
import SwiftUI
import VietEngine

/// Bảng màu và thành phần giao diện theo thiết kế "KVietKey — Giao diện".
enum KV {
    static let blue = Color(hex: 0x1F5BFF)
    static let navy = Color(hex: 0x0B2A6F)
    static let ink = Color(hex: 0x0B1B3A)
    static let muted = Color(hex: 0x5B6B8C)
    static let text2 = Color(hex: 0x3C4D72)
    static let bg = Color(hex: 0xF4F8FF)
    static let border = Color(hex: 0xE3EAF7)
    static let border2 = Color(hex: 0xD9E4F7)
    static let keyBorder = Color(hex: 0xC7D3EA)
    static let soft = Color(hex: 0xEEF3FF)
    static let soft2 = Color(hex: 0xE3EDFF)
    static let banner = Color(hex: 0xE8F0FF)
    static let cyan = Color(hex: 0x7DE3F5)
    static let cyanDot = Color(hex: 0x00B8D9)
    static let cyanSoft = Color(hex: 0xCFF3FA)
    static let cyanText = Color(hex: 0x0A5C6E)
    static let switchOff = Color(hex: 0xAEBBD6)
    static let navyText = Color(hex: 0xC4D4FF)
    static let tiles: [(bg: Color, fg: Color)] = [
        (Color(hex: 0xE3EDFF), Color(hex: 0x1F5BFF)),
        (Color(hex: 0xCFF3FA), Color(hex: 0x0A5C6E)),
        (Color(hex: 0xDDE4FF), Color(hex: 0x2B3FA8)),
    ]
}

extension Color {
    init(hex: UInt32, opacity: Double = 1) {
        self.init(.sRGB,
                  red: Double((hex >> 16) & 0xFF) / 255,
                  green: Double((hex >> 8) & 0xFF) / 255,
                  blue: Double(hex & 0xFF) / 255,
                  opacity: opacity)
    }
}

extension Font {
    private static let hasBeVietnamPro = NSFontManager.shared.availableFontFamilies.contains("Be Vietnam Pro")

    /// Be Vietnam Pro nếu máy đã cài, nếu không dùng phông hệ thống.
    static func kv(_ size: CGFloat, _ weight: Font.Weight = .regular) -> Font {
        hasBeVietnamPro ? .custom("Be Vietnam Pro", size: size).weight(weight) : .system(size: size, weight: weight)
    }
}

/// Logo KVietKey: nền xanh bo góc, chữ V trắng và nét dấu màu ngọc.
struct KLogo: View {
    var size: CGFloat = 40

    var body: some View {
        Canvas { ctx, canvasSize in
            let s = canvasSize.width / 40
            let rect = CGRect(origin: .zero, size: canvasSize)
            ctx.fill(Path(roundedRect: rect, cornerRadius: 11 * s), with: .color(KV.blue))
            var v = Path()
            v.move(to: CGPoint(x: 12 * s, y: 15 * s))
            v.addLine(to: CGPoint(x: 20 * s, y: 29 * s))
            v.addLine(to: CGPoint(x: 28 * s, y: 15 * s))
            ctx.stroke(v, with: .color(.white), style: StrokeStyle(lineWidth: 3.6 * s, lineCap: .round, lineJoin: .round))
            var mark = Path()
            mark.move(to: CGPoint(x: 24 * s, y: 9 * s))
            mark.addLine(to: CGPoint(x: 28.5 * s, y: 6.5 * s))
            ctx.stroke(mark, with: .color(KV.cyan), style: StrokeStyle(lineWidth: 2.8 * s, lineCap: .round))
        }
        .frame(width: size, height: size)
        .accessibilityLabel("Logo KVietKey")
    }
}

/// Công tắc 42×24.
struct KSwitch: View {
    @Binding var isOn: Bool
    var label: String = ""

    var body: some View {
        Button { isOn.toggle() } label: {
            ZStack(alignment: .leading) {
                Capsule().fill(isOn ? KV.blue : KV.switchOff)
                Circle().fill(Color.white)
                    .shadow(color: KV.ink.opacity(0.3), radius: 1, y: 1)
                    .frame(width: 18, height: 18)
                    .offset(x: isOn ? 21 : 3)
            }
            .frame(width: 42, height: 24)
            .animation(.easeInOut(duration: 0.15), value: isOn)
        }
        .buttonStyle(.plain)
        .accessibilityLabel(label)
        .accessibilityValue(isOn ? "On" : "Off")
    }
}

struct KCard<Content: View>: View {
    var padding = EdgeInsets(top: 18, leading: 20, bottom: 18, trailing: 20)
    var spacing: CGFloat = 12
    var fillHeight = false
    @ViewBuilder var content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: spacing) { content }
            .padding(padding)
            .frame(maxWidth: .infinity, maxHeight: fillHeight ? .infinity : nil, alignment: .topLeading)
            .background(RoundedRectangle(cornerRadius: 18).fill(Color.white))
            .overlay(RoundedRectangle(cornerRadius: 18).stroke(KV.border, lineWidth: 1))
    }
}

/// Dòng cài đặt có công tắc, dùng trong thẻ.
struct KToggleRow: View {
    let title: String
    var subtitle: String?
    @Binding var isOn: Bool
    var divider = true
    var height: CGFloat = 52

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: 12) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(title).font(.kv(14, .medium)).foregroundStyle(KV.ink)
                    if let subtitle { Text(subtitle).font(.kv(12)).foregroundStyle(KV.muted) }
                }
                Spacer(minLength: 8)
                KSwitch(isOn: $isOn, label: title)
            }
            .frame(minHeight: height)
            if divider { Rectangle().fill(KV.soft).frame(height: 1) }
        }
    }
}

struct KeyCap: View {
    let text: String
    var large = true

    var body: some View {
        Text(text)
            .font(.kv(large ? 15 : 12, .semibold))
            .foregroundStyle(KV.ink)
            .frame(minWidth: large ? 40 : 22, minHeight: large ? 36 : 22)
            .padding(.horizontal, large ? 8 : 4)
            .background(RoundedRectangle(cornerRadius: large ? 10 : 6).fill(large ? Color.white : KV.bg))
            .overlay(RoundedRectangle(cornerRadius: large ? 10 : 6).stroke(KV.keyBorder, lineWidth: 1))
            .shadow(color: large ? KV.keyBorder : .clear, radius: 0, y: 2)
    }
}

struct KeyCaps: View {
    let hotkey: Hotkey
    var large = true

    var body: some View {
        HStack(spacing: 4) {
            ForEach(Array(hotkey.symbols.enumerated()), id: \.offset) { _, s in KeyCap(text: s, large: large) }
        }
    }
}

enum KSegmentedStyle { case light, solid }

/// Nhóm nút chọn một (Việt/Anh, kiểu gõ…).
struct KSegmented<T: Hashable>: View {
    let options: [(value: T, label: String)]
    @Binding var selection: T
    var height: CGFloat = 34
    var fontSize: CGFloat = 14
    var radius: CGFloat = 12
    var style: KSegmentedStyle = .light

    var body: some View {
        HStack(spacing: 4) {
            ForEach(Array(options.enumerated()), id: \.offset) { _, option in
                let selected = option.value == selection
                Button { selection = option.value } label: {
                    Text(option.label)
                        .font(.kv(fontSize, .semibold))
                        .frame(maxWidth: .infinity, minHeight: height)
                        .foregroundStyle(selected ? (style == .solid ? Color.white : KV.blue) : KV.text2)
                        .background(
                            RoundedRectangle(cornerRadius: radius - 3)
                                .fill(selected ? (style == .solid ? KV.blue : Color.white) : Color.clear)
                                .shadow(color: selected ? KV.ink.opacity(0.18) : .clear, radius: 2, y: 1)
                        )
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
            }
        }
        .padding(4)
        .background(RoundedRectangle(cornerRadius: radius).fill(KV.soft))
    }
}

struct KPrimaryButton: ButtonStyle {
    var height: CGFloat = 44
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.kv(height > 44 ? 15 : 14, .semibold))
            .foregroundStyle(Color.white)
            .padding(.horizontal, height > 44 ? 22 : 18)
            .frame(height: height)
            .background(RoundedRectangle(cornerRadius: height > 44 ? 14 : 12).fill(KV.blue))
            .shadow(color: KV.blue.opacity(height > 44 ? 0.32 : 0), radius: 8, y: 4)
            .opacity(configuration.isPressed ? 0.85 : 1)
    }
}

struct KOutlineButton: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.kv(13, .semibold))
            .foregroundStyle(KV.blue)
            .padding(.horizontal, 14)
            .frame(height: 38)
            .background(RoundedRectangle(cornerRadius: 10).fill(Color.white))
            .overlay(RoundedRectangle(cornerRadius: 10).stroke(KV.keyBorder, lineWidth: 1))
            .opacity(configuration.isPressed ? 0.75 : 1)
    }
}

struct KTextButton: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.kv(15, .semibold))
            .foregroundStyle(KV.blue)
            .padding(.horizontal, 18)
            .frame(height: 48)
            .opacity(configuration.isPressed ? 0.6 : 1)
    }
}

struct KSearchField: View {
    let placeholder: String
    @Binding var text: String

    var body: some View {
        HStack(spacing: 10) {
            Image(systemName: "magnifyingglass").foregroundStyle(KV.muted)
            TextField(placeholder, text: $text)
                .textFieldStyle(.plain)
                .font(.kv(14))
                .foregroundStyle(KV.ink)
        }
        .padding(.horizontal, 14)
        .frame(height: 44)
        .background(RoundedRectangle(cornerRadius: 12).fill(Color.white))
        .overlay(RoundedRectangle(cornerRadius: 12).stroke(KV.border2, lineWidth: 1))
    }
}

/// Ô thả xuống (bảng mã…) dạng nút có mũi tên.
struct KDropdown<T: Hashable>: View {
    let options: [(value: T, label: String)]
    @Binding var selection: T
    var height: CGFloat = 44

    var body: some View {
        Menu {
            ForEach(Array(options.enumerated()), id: \.offset) { _, o in
                Button(o.label) { selection = o.value }
            }
        } label: {
            HStack {
                Text(options.first { $0.value == selection }?.label ?? "")
                    .font(.kv(14, .medium)).foregroundStyle(KV.ink)
                Spacer()
                Image(systemName: "chevron.down").font(.system(size: 12, weight: .semibold)).foregroundStyle(KV.muted)
            }
            .padding(.horizontal, 14)
            .frame(height: height)
            .background(RoundedRectangle(cornerRadius: 12).fill(KV.bg))
            .overlay(RoundedRectangle(cornerRadius: 12).stroke(KV.border2, lineWidth: 1))
            .contentShape(Rectangle())
        }
        .menuStyle(.borderlessButton)
        .menuIndicator(.hidden)
    }
}

/// Lưới 4 thẻ chọn kiểu gõ (Telex, VNI, VIQR, Kết hợp).
struct MethodCards: View {
    @Binding var selection: InputMethod

    var body: some View {
        HStack(spacing: 12) {
            ForEach(InputMethod.allCases, id: \.self) { m in
                let selected = selection == m
                Button { selection = m } label: {
                    VStack(alignment: .leading, spacing: 6) {
                        Text(m.title).font(.kv(16, .bold)).foregroundStyle(selected ? KV.blue : KV.ink)
                        Text(m.example).font(.kv(13)).foregroundStyle(selected ? KV.text2 : KV.muted)
                            .multilineTextAlignment(.leading)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                    .padding(14)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                    .background(RoundedRectangle(cornerRadius: 14).fill(selected ? KV.soft : Color.white))
                    .overlay(RoundedRectangle(cornerRadius: 14).stroke(selected ? KV.blue : KV.border, lineWidth: 2))
                    .overlay(alignment: .topTrailing) {
                        if selected {
                            Image(systemName: "checkmark")
                                .font(.system(size: 10, weight: .heavy))
                                .foregroundStyle(Color.white)
                                .frame(width: 20, height: 20)
                                .background(Circle().fill(KV.blue))
                                .padding(10)
                        }
                    }
                    .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
            }
        }
        .fixedSize(horizontal: false, vertical: true)
    }
}

extension InputMethod {
    var title: String { self == .combined ? tr("Kết hợp", "Combined") : displayName }

    var example: String {
        switch self {
        case .telex: return "aa → â · s → sắc"
        case .vni: return "a6 → â · 1 → sắc"
        case .viqr: return "a^ → â · ' → sắc"
        case .combined: return tr("Telex + VNI cùng lúc", "Telex + VNI together")
        }
    }
}

extension Hotkey {
    var symbols: [String] {
        var s: [String] = []
        if modifiers & CGEventFlags.maskControl.rawValue != 0 { s.append("⌃") }
        if modifiers & CGEventFlags.maskAlternate.rawValue != 0 { s.append("⌥") }
        if modifiers & CGEventFlags.maskShift.rawValue != 0 { s.append("⇧") }
        if modifiers & CGEventFlags.maskCommand.rawValue != 0 { s.append("⌘") }
        if let k = keyCode { s.append(KeyCode.name(for: k)) }
        return s
    }
}

extension Charset {
    static var options: [(value: Charset, label: String)] { allCases.map { ($0, $0.displayName) } }
}
