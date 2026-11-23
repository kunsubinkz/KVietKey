namespace KVietKey.Engine;

/// <summary>Kiểm tra âm tiết tiếng Việt theo cấu trúc: phụ âm đầu + vần + phụ âm cuối + ràng buộc dấu.</summary>
internal static class SpellChecker
{
    private static readonly HashSet<string> Initials =
    [
        "b", "c", "d", "đ", "g", "h", "k", "l", "m", "n", "p", "q", "r", "s", "t", "v", "x",
        "ch", "gh", "gi", "kh", "ng", "nh", "ph", "qu", "th", "tr", "ngh",
    ];

    private static readonly HashSet<string> ForeignInitials = ["z", "f", "w", "j"];

    private static readonly HashSet<string> Nuclei =
    [
        "a", "ă", "â", "e", "ê", "i", "o", "ô", "ơ", "u", "ư", "y",
        "ai", "ao", "au", "âu", "ay", "ây", "eo", "êu", "ia", "iê", "iu", "oa", "oă", "oe", "oi",
        "ôi", "ơi", "ua", "uâ", "uê", "ui", "uô", "uơ", "ưa", "ưi", "ưu", "uy", "ươ", "yê", "oo",
        "iêu", "yêu", "oai", "oao", "oay", "oeo", "uây", "uôi", "ươi", "ươu", "uyê", "uya", "uyu",
    ];

    private static readonly HashSet<string> NoFinal = ["ia", "ua", "ưa", "uya"];
    private static readonly HashSet<string> NeedsFinal = ["iê", "yê", "uô", "ươ", "uyê"];
    private static readonly HashSet<string> Finals = ["", "c", "ch", "m", "n", "ng", "nh", "p", "t"];
    private static readonly HashSet<string> StopFinals = ["c", "ch", "p", "t"];

    /// <summary>Âm tiết mượn từ tiếng dân tộc thiểu số (Đắk Lắk, Đắk Nông) có vần kết thúc bằng <c>k</c>.</summary>
    private static readonly HashSet<string> LoanSyllables = ["đăk", "lăk", "đak", "lak", "nông"];

    // Thứ tự không ảnh hưởng kết quả (chỉ cần có ứng viên khớp).
    private static readonly string[] NativeCandidates =
        Initials.Where(i => i != "q").Append("").ToArray();

    private static readonly string[] AllCandidates =
        NativeCandidates.Concat(ForeignInitials).ToArray();

    private static bool IsVowelLabel(char c) => "aăâeêioôơuưy".Contains(c);

    /// <summary>Phụ âm đầu hiện có (đến nguyên âm đầu tiên) có thể là phụ âm đầu tiếng Việt không.</summary>
    public static bool InitialIsValid(IReadOnlyList<VChar> chars, bool allowForeign)
    {
        Span<char> prefix = stackalloc char[chars.Count];
        int n = 0;
        foreach (var c in chars)
        {
            if (c.IsVowel) break;
            prefix[n++] = c.Label;
        }
        if (n == 0) return true;
        var s = new string(prefix[..n]);
        if (Initials.Contains(s)) return true;
        return allowForeign && ForeignInitials.Contains(s);
    }

    /// <summary>Kiểm tra toàn bộ từ hiện tại có phải một âm tiết tiếng Việt hợp lệ.</summary>
    public static bool IsValid(IReadOnlyList<VChar> chars, Tone tone, bool allowForeign)
    {
        if (chars.Count == 0) return true;
        var word = new char[chars.Count];
        bool anyVowel = false;
        for (int i = 0; i < chars.Count; i++)
        {
            word[i] = chars[i].Label;
            if (!char.IsLetter(word[i])) return false;
            anyVowel |= chars[i].IsVowel;
        }
        if (!anyVowel) return true;
        if ((tone is Tone.None or Tone.Acute or Tone.Dot) && LoanSyllables.Contains(new string(word))) return true;

        bool stopOnly = tone is not (Tone.None or Tone.Acute or Tone.Dot);
        var wordSpan = new ReadOnlySpan<char>(word);
        foreach (var initial in allowForeign ? AllCandidates : NativeCandidates)
        {
            if (!wordSpan.StartsWith(initial, StringComparison.Ordinal)) continue;
            var rest = wordSpan[initial.Length..];
            if (rest.IsEmpty) continue;
            if (Matches(initial, rest, stopOnly)) return true;
        }
        return false;
    }

    private static bool Matches(string initial, ReadOnlySpan<char> rest, bool stopOnly)
    {
        int maxLen = Math.Min(3, rest.Length);
        while (maxLen >= 1)
        {
            var nucleus = new string(rest[..maxLen]);
            maxLen--;
            if (!Nuclei.Contains(nucleus) || !nucleus.All(IsVowelLabel)) continue;
            var fin = new string(rest[nucleus.Length..]);
            if (!Finals.Contains(fin)) continue;
            if (fin.Length > 0 && NoFinal.Contains(nucleus)) continue;
            if (fin.Length > 0 && nucleus.Length > 1 && "iouy".Contains(nucleus[^1]) && nucleus != "uy") continue;
            if (fin.Length == 0 && NeedsFinal.Contains(nucleus)) continue;
            if (stopOnly && StopFinals.Contains(fin)) continue;
            if (!InitialAllows(initial, nucleus[0])) continue;
            return true;
        }
        return false;
    }

    private static bool InitialAllows(string initial, char first) => initial switch
    {
        "k" => first is 'e' or 'ê' or 'i' or 'y',
        "gh" or "ngh" => first is 'e' or 'ê' or 'i',
        "c" => first is not ('e' or 'ê' or 'i' or 'y'),
        "g" => first is not ('e' or 'ê'),
        "ng" => first is not ('e' or 'ê' or 'i'),
        _ => true,
    };
}
