import Foundation

/// Kiểm tra âm tiết tiếng Việt theo cấu trúc: phụ âm đầu + vần + phụ âm cuối + ràng buộc dấu.
enum SpellChecker {
    private static let initials: Set<String> = [
        "b", "c", "d", "đ", "g", "h", "k", "l", "m", "n", "p", "q", "r", "s", "t", "v", "x",
        "ch", "gh", "gi", "kh", "ng", "nh", "ph", "qu", "th", "tr", "ngh",
    ]
    private static let foreignInitials: Set<String> = ["z", "f", "w", "j"]

    private static let nuclei: Set<String> = [
        "a", "ă", "â", "e", "ê", "i", "o", "ô", "ơ", "u", "ư", "y",
        "ai", "ao", "au", "âu", "ay", "ây", "eo", "êu", "ia", "iê", "iu", "oa", "oă", "oe", "oi",
        "ôi", "ơi", "ua", "uâ", "uê", "ui", "uô", "uơ", "ưa", "ưi", "ưu", "uy", "ươ", "yê", "oo",
        "iêu", "yêu", "oai", "oao", "oay", "oeo", "uây", "uôi", "ươi", "ươu", "uyê", "uya", "uyu",
    ]
    private static let noFinal: Set<String> = ["ia", "ua", "ưa", "uya"]
    private static let needsFinal: Set<String> = ["iê", "yê", "uô", "ươ", "uyê"]
    private static let finals: Set<String> = ["", "c", "ch", "m", "n", "ng", "nh", "p", "t"]
    private static let stopFinals: Set<String> = ["c", "ch", "p", "t"]

    /// Âm tiết mượn từ tiếng dân tộc thiểu số (Đắk Lắk, Đắk Nông) có vần kết thúc bằng `k`.
    private static let loanSyllables: Set<String> = ["đăk", "lăk", "đak", "lak", "nông"]

    private static func isVowelLabel(_ c: Character) -> Bool {
        "aăâeêioôơuưy".contains(c)
    }

    /// Phụ âm đầu hiện có (đến nguyên âm đầu tiên) có thể là phụ âm đầu tiếng Việt không.
    static func initialIsValid(_ chars: [VChar], allowForeign: Bool) -> Bool {
        var prefix = ""
        for c in chars {
            if c.isVowel { break }
            prefix.append(c.label)
        }
        if prefix.isEmpty { return true }
        if initials.contains(prefix) { return true }
        if allowForeign, foreignInitials.contains(prefix) { return true }
        return false
    }

    /// Kiểm tra toàn bộ từ hiện tại có phải một âm tiết tiếng Việt hợp lệ.
    static func isValid(_ chars: [VChar], tone: Tone, allowForeign: Bool) -> Bool {
        if chars.isEmpty { return true }
        let labels = chars.map { $0.label }
        if labels.contains(where: { !$0.isLetter }) { return false }
        if !chars.contains(where: { $0.isVowel }) { return true }
        if tone == .none || tone == .acute || tone == .dot, loanSyllables.contains(String(labels)) { return true }
        let word = Array(labels)
        let stopOnly = tone != .none && tone != .acute && tone != .dot

        var candidates: [String] = initials.filter { $0 != "q" }.sorted()
        if allowForeign { candidates += foreignInitials.sorted() }
        candidates.append("")
        for initial in candidates {
            let ic = Array(initial)
            guard word.count >= ic.count, Array(word[0..<ic.count]) == ic else { continue }
            let rest = Array(word[ic.count...])
            if rest.isEmpty { continue }
            if matches(initial: initial, rest: rest, stopOnly: stopOnly) { return true }
        }
        return false
    }

    private static func matches(initial: String, rest: [Character], stopOnly: Bool) -> Bool {
        var maxLen = min(3, rest.count)
        while maxLen >= 1 {
            let nucleus = String(rest[0..<maxLen])
            maxLen -= 1
            guard nuclei.contains(nucleus), nucleus.allSatisfy(isVowelLabel) else { continue }
            let fin = String(rest[(nucleus.count)...])
            guard finals.contains(fin) else { continue }
            if !fin.isEmpty, noFinal.contains(nucleus) { continue }
            if !fin.isEmpty, nucleus.count > 1, "iouy".contains(nucleus.last!), nucleus != "uy" { continue }
            if fin.isEmpty, needsFinal.contains(nucleus) { continue }
            if stopOnly, stopFinals.contains(fin) { continue }
            if !initialAllows(initial, first: nucleus.first!) { continue }
            return true
        }
        return false
    }

    private static func initialAllows(_ initial: String, first: Character) -> Bool {
        let front: Set<Character> = ["e", "ê", "i", "y"]
        switch initial {
        case "k": return front.contains(first)
        case "gh", "ngh": return ["e", "ê", "i"].contains(first)
        case "c": return !front.contains(first)
        case "g": return !["e", "ê"].contains(first)
        case "ng": return !["e", "ê", "i"].contains(first)
        default: return true
        }
    }
}
