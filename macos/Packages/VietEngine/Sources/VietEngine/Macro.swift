import Foundation

/// Bảng gõ tắt: `viết_tắt` → `nội dung`.
public struct MacroTable: Codable, Equatable, Sendable {
    public var entries: [String: String] = [:]

    public init(entries: [String: String] = [:]) {
        self.entries = entries
    }

    public var isEmpty: Bool { entries.isEmpty }

    /// Tìm nội dung mở rộng cho `typed` (chuỗi phím đã gõ).
    /// Khi `keepCase` bật: `VN` → toàn bộ hoa, `Vn` → viết hoa chữ đầu.
    public func expand(_ typed: String, keepCase: Bool) -> String? {
        if let exact = entries[typed] { return exact }
        guard keepCase else { return nil }
        let lower = typed.lowercased()
        guard let value = entries[lower] else { return nil }
        let letters = typed.filter { $0.isLetter }
        if letters.count >= 2, letters.allSatisfy({ $0.isUppercase }) { return value.uppercased() }
        if let first = typed.first, first.isUppercase {
            return value.prefix(1).uppercased() + value.dropFirst()
        }
        return value
    }

    // MARK: Import / Export (.txt, mỗi dòng `viết_tắt:nội dung`)

    public static func parse(text: String) -> MacroTable {
        var table = MacroTable()
        for line in text.split(whereSeparator: \.isNewline) {
            guard let idx = line.firstIndex(of: ":") else { continue }
            let key = line[..<idx].trimmingCharacters(in: .whitespaces)
            let value = line[line.index(after: idx)...].trimmingCharacters(in: .whitespaces)
            if !key.isEmpty, !value.isEmpty { table.entries[key] = value }
        }
        return table
    }

    public func exportText() -> String {
        entries.keys.sorted().map { "\($0):\(entries[$0]!)" }.joined(separator: "\n") + "\n"
    }

    public mutating func merge(_ other: MacroTable) {
        entries.merge(other.entries) { _, new in new }
    }

    // MARK: Lưu trữ JSON

    public static func load(from url: URL) -> MacroTable {
        guard let data = try? Data(contentsOf: url),
              let table = try? JSONDecoder().decode(MacroTable.self, from: data) else { return MacroTable() }
        return table
    }

    public func save(to url: URL) throws {
        try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try encoder.encode(self).write(to: url, options: .atomic)
    }
}
