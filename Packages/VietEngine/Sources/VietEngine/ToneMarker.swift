import Foundation

/// Quy tắc đặt dấu thanh.
enum ToneMarker {
    /// Chỉ số các nguyên âm tạo thành vần của âm tiết (bỏ `u` của `qu`, `i` của `gi`).
    static func vowelRun(_ chars: [VChar]) -> [Int] {
        var runs: [[Int]] = []
        var current: [Int] = []
        for (i, c) in chars.enumerated() {
            var vowel = c.isVowel
            if vowel, c.base == "u", i > 0, chars[i - 1].base == "q" { vowel = false }
            if vowel, c.base == "i", i > 0, chars[i - 1].base == "g",
               i + 1 < chars.count, chars[i + 1].isVowel { vowel = false }
            if vowel {
                current.append(i)
            } else if !current.isEmpty {
                runs.append(current)
                current = []
            }
        }
        if !current.isEmpty { runs.append(current) }
        return runs.last ?? []
    }

    /// Vị trí nguyên âm mang dấu thanh.
    static func position(_ chars: [VChar], modern: Bool) -> Int? {
        let run = vowelRun(chars)
        if run.isEmpty { return nil }
        if run.count == 1 { return run[0] }
        if let marked = run.last(where: { chars[$0].mark != .none && chars[$0].mark != .stroke }) {
            return marked
        }
        if run.count >= 3 { return run[1] }
        let hasFinal = (run.last! + 1) < chars.count
        if hasFinal { return run[1] }
        let pair = String([chars[run[0]].base, chars[run[1]].base])
        if modern, ["oa", "oe", "uy"].contains(pair) { return run[1] }
        return run[0]
    }
}
