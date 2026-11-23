namespace KVietKey.Engine;

/// <summary>Quy tắc đặt dấu thanh.</summary>
internal static class ToneMarker
{
    /// <summary>Chỉ số các nguyên âm tạo thành vần của âm tiết (bỏ <c>u</c> của <c>qu</c>, <c>i</c> của <c>gi</c>).</summary>
    public static List<int> VowelRun(IReadOnlyList<VChar> chars)
    {
        List<int> last = [];
        List<int> current = [];
        for (int i = 0; i < chars.Count; i++)
        {
            var c = chars[i];
            bool vowel = c.IsVowel;
            if (vowel && c.Base == 'u' && i > 0 && chars[i - 1].Base == 'q') vowel = false;
            if (vowel && c.Base == 'i' && i > 0 && chars[i - 1].Base == 'g'
                && i + 1 < chars.Count && chars[i + 1].IsVowel) vowel = false;
            if (vowel)
            {
                current.Add(i);
            }
            else if (current.Count > 0)
            {
                last = current;
                current = [];
            }
        }
        return current.Count > 0 ? current : last;
    }

    /// <summary>Vị trí nguyên âm mang dấu thanh.</summary>
    public static int? Position(IReadOnlyList<VChar> chars, bool modern)
    {
        var run = VowelRun(chars);
        if (run.Count == 0) return null;
        if (run.Count == 1) return run[0];
        for (int k = run.Count - 1; k >= 0; k--)
        {
            var mark = chars[run[k]].Mark;
            if (mark != Mark.None && mark != Mark.Stroke) return run[k];
        }
        if (run.Count >= 3) return run[1];
        bool hasFinal = run[^1] + 1 < chars.Count;
        if (hasFinal) return run[1];
        if (modern)
        {
            var pair = (chars[run[0]].Base, chars[run[1]].Base);
            if (pair is ('o', 'a') or ('o', 'e') or ('u', 'y')) return run[1];
        }
        return run[0];
    }
}
