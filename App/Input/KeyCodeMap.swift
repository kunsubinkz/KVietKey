import Foundation

/// Mã phím ảo (kVK_*) dùng trong bộ gõ.
enum KeyCode {
    static let returnKey = 36
    static let tab = 48
    static let space = 49
    static let delete = 51
    static let escape = 53
    static let keypadEnter = 76
    static let forwardDelete = 117
    static let home = 115
    static let end = 119
    static let pageUp = 116
    static let pageDown = 121
    static let leftArrow = 123
    static let rightArrow = 124
    static let downArrow = 125
    static let upArrow = 126

    /// Phím điều hướng/chỉnh sửa làm con trỏ rời khỏi từ đang gõ.
    static let navigation: Set<Int> = [escape, forwardDelete, home, end, pageUp, pageDown,
                                       leftArrow, rightArrow, downArrow, upArrow]
    /// Phím kết thúc từ nhưng vẫn được chuyển tiếp nguyên vẹn.
    static let wordBreakers: Set<Int> = [returnKey, keypadEnter, tab]

    private static let names: [Int: String] = [
        space: "Space", returnKey: "↩", tab: "⇥", delete: "⌫", escape: "⎋",
        0: "A", 11: "B", 8: "C", 2: "D", 14: "E", 3: "F", 5: "G", 4: "H", 34: "I", 38: "J",
        40: "K", 37: "L", 46: "M", 45: "N", 31: "O", 35: "P", 12: "Q", 15: "R", 1: "S", 17: "T",
        32: "U", 9: "V", 13: "W", 7: "X", 16: "Y", 6: "Z",
        18: "1", 19: "2", 20: "3", 21: "4", 23: "5", 22: "6", 26: "7", 28: "8", 25: "9", 29: "0",
        50: "`", 27: "-", 24: "=", 33: "[", 30: "]", 42: "\\", 41: ";", 39: "'", 43: ",", 47: ".", 44: "/",
    ]

    static func name(for code: Int) -> String { names[code] ?? "#\(code)" }
}
