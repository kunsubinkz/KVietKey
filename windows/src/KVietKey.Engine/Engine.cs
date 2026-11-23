using System.Globalization;
using System.Text;

namespace KVietKey.Engine;

/// <summary>Tách văn bản thành các cụm ký tự hiển thị (grapheme) — tương đương <c>Character</c> của Swift.</summary>
internal static class TextElements
{
    public static List<string> Split(string text)
    {
        var list = new List<string>(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            int len = char.IsAscii(text[i]) && (i + 1 >= text.Length || text[i + 1] < '\u0300')
                ? 1
                : StringInfo.GetNextTextElementLength(text.AsSpan(i));
            list.Add(text.Substring(i, len));
            i += len;
        }
        return list;
    }

    /// <summary>So sánh theo tương đương chính tắc (Unicode canonical equivalence), như <c>Character ==</c> của Swift.</summary>
    public static bool Equal(string a, string b) =>
        string.Equals(a, b, StringComparison.Ordinal)
        || string.Equals(a.Normalize(NormalizationForm.FormC), b.Normalize(NormalizationForm.FormC), StringComparison.Ordinal);

    public static int CodePoints(string text)
    {
        int n = 0;
        foreach (var _ in text.EnumerateRunes()) n++;
        return n;
    }
}

/// <summary>Bộ máy gõ tiếng Việt: nhận từng ký tự, trả về thao tác xóa/gõ cần thực hiện.</summary>
public sealed class Engine
{
    private enum CapState { None, AfterPunct, Armed }

    public EngineConfig Config { get; set; }
    public MacroTable Macros { get; set; } = new();

    private List<VChar> _chars = [];
    private Tone _tone = Tone.None;
    private readonly List<char> _raw = [];
    private HashSet<char> _undone = [];
    private bool _rawReliable = true;
    /// <summary>Văn bản (đã mã hóa theo bảng mã đích) của từ hiện tại đang có trên màn hình.</summary>
    private string _onScreen = "";
    private CapState _capState = CapState.None;

    public Engine(EngineConfig? config = null)
    {
        Config = config ?? new EngineConfig();
    }

    // Trạng thái

    /// <summary>Từ đang gõ (Unicode), phục vụ debug/test.</summary>
    public string CurrentWord => RenderUnicode();

    public bool HasPendingWord => _chars.Count > 0;

    /// <summary>Xóa bộ đệm: dùng khi click chuột, đổi ứng dụng, phím mũi tên, Ctrl/Alt + phím…</summary>
    public void Reset()
    {
        ResetWord();
        _capState = CapState.None;
    }

    private void ResetWord()
    {
        _chars.Clear();
        _tone = Tone.None;
        _raw.Clear();
        _undone.Clear();
        _rawReliable = true;
        _onScreen = "";
    }

    // Xử lý phím

    /// <summary>
    /// Phím này thuộc về từ đang gõ (chữ cái ASCII hoặc phím dấu của kiểu gõ hiện tại) nên cần gọi <see cref="Process(Rune)"/>;
    /// ngược lại là phím ngắt từ và cần gọi <see cref="ProcessBreak(Rune)"/>.
    /// </summary>
    public bool IsComposingKey(Rune input) =>
        input.IsBmp && (char.IsAsciiLetter((char)input.Value)
            || (Config.Method.ActionFor((char)input.Value) != null && !Rune.IsWhiteSpace(input)));

    /// <summary>Xử lý một ký tự người dùng gõ (đã tính Shift/CapsLock).</summary>
    public EngineResult Process(Rune input) =>
        input.IsBmp ? Process((char)input.Value) : ProcessBreak(input);

    public EngineResult Process(char input)
    {
        char key = input;
        bool isAsciiLetter = char.IsAsciiLetter(input);

        if (isAsciiLetter && _chars.Count == 0 && _capState == CapState.Armed)
        {
            key = char.ToUpperInvariant(input);
            _capState = CapState.None;
        }
        else if (isAsciiLetter && _capState != CapState.None)
        {
            _capState = CapState.None;
        }

        char lower = char.ToLowerInvariant(key);
        if (Config.Method.ActionFor(lower) is { } action && !_undone.Contains(lower))
        {
            var backupChars = new List<VChar>(_chars);
            var backupTone = _tone;
            var backupUndone = new HashSet<char>(_undone);
            if (Apply(action, key))
            {
                _raw.Add(key);
                return Refresh(input);
            }
            _chars = backupChars;
            _tone = backupTone;
            _undone = backupUndone;
        }

        if (!isAsciiLetter) return ProcessBreak(input);
        AppendLiteral(key);
        _raw.Add(key);
        return Refresh(input);
    }

    /// <summary>
    /// Xử lý phím ngắt từ (dấu cách, Enter, Tab, dấu câu…). Phần thay thế (nếu có) phải được gõ
    /// trước khi chuyển tiếp phím gốc.
    /// </summary>
    public EngineResult ProcessBreak(Rune input) =>
        ProcessBreak(input.IsBmp ? (char)input.Value : '\uFFFF');

    public EngineResult ProcessBreak(char input)
    {
        var result = EngineResult.Pass;
        if (_chars.Count > 0)
        {
            if (Config.MacroEnabled && MacroExpansion() is { } expansion)
            {
                result = Replace(expansion);
            }
            else if (Config.AutoRestore && _rawReliable && _undone.Count == 0 && ShouldRestore())
            {
                result = Replace(new string(_raw.ToArray()));
            }
        }
        ResetWord();
        UpdateCapState(input);
        return result;
    }

    /// <summary>Người dùng nhấn Backspace (phím vẫn được chuyển tiếp tới ứng dụng).</summary>
    public void Backspace()
    {
        if (_chars.Count == 0) return;
        if (!Config.Charset.SingleBackspacePerLetter())
        {
            ResetWord();
            return;
        }
        // Windows chỉ xóa một code point mỗi lần Backspace: nếu chữ cuối gồm nhiều code point (Unicode tổ hợp)
        // thì ứng dụng chỉ mất dấu, không mất cả chữ. Bỏ theo dõi từ này để không lệch với màn hình.
        if (Config.BackspacePerCodePoint)
        {
            var elements = TextElements.Split(_onScreen);
            if (elements.Count > 0 && TextElements.CodePoints(elements[^1]) > 1)
            {
                ResetWord();
                return;
            }
        }
        _chars = DisplayChars();
        _chars.RemoveAt(_chars.Count - 1);
        if (!_chars.Any(c => c.IsVowel)) _tone = Tone.None;
        _raw.Clear();
        _raw.AddRange(_chars.Select(c => c.Base));
        _rawReliable = false;
        _onScreen = Config.Charset.Encode(RenderUnicode());
        if (_chars.Count == 0) ResetWord();
    }

    // Áp dụng hành động

    private bool Apply(KeyAction action, char key) => action.Kind switch
    {
        ActionKind.Tone => ApplyTone(action.Tone, key),
        ActionKind.ClearTone => ClearTone(),
        ActionKind.DoubleVowel => ApplyDoubleVowel(action.Vowel, key),
        ActionKind.DoubleD => ApplyDoubleD(key),
        ActionKind.W => ApplyHornBreve(HornKind.W, key),
        ActionKind.Circumflex => ApplyCircumflex(key),
        ActionKind.Horn => ApplyHornBreve(HornKind.Horn, key),
        ActionKind.Breve => ApplyHornBreve(HornKind.Breve, key),
        ActionKind.Stroke => ApplyStroke(key),
        _ => false,
    };

    private bool ClearTone()
    {
        if (_tone == Tone.None) return false;
        _tone = Tone.None;
        return true;
    }

    private void AppendLiteral(char key) =>
        _chars.Add(new VChar(char.ToLowerInvariant(key), Upper: char.IsUpper(key)));

    private bool SpellGate() =>
        !Config.SpellCheck || SpellChecker.InitialIsValid(_chars, Config.AllowZFWJ);

    private bool Undo(char key)
    {
        AppendLiteral(key);
        _undone.Add(char.ToLowerInvariant(key));
        return true;
    }

    private void SetMark(int index, Mark mark) => _chars[index] = _chars[index] with { Mark = mark };

    private bool ApplyTone(Tone t, char key)
    {
        if (!_chars.Any(c => c.IsVowel) || !SpellGate()) return false;
        if (!Config.FreeMarking && _chars.Count > 0 && !_chars[^1].IsVowel) return false;
        if (_tone == t)
        {
            // Chỉ hoàn tác khi gõ lặp phím dấu ngay liền sau (ss, ff…). Lặp cách xa (response, order) là chữ thường,
            // để nguyên ký tự rồi khôi phục khi ngắt từ.
            if (_raw.Count == 0 || char.ToLowerInvariant(_raw[^1]) != char.ToLowerInvariant(key)) return false;
            _tone = Tone.None;
            return Undo(key);
        }
        _tone = t;
        return true;
    }

    private bool ApplyDoubleVowel(char v, char key)
    {
        if (_chars.Count == 0 || !SpellGate()) return false;
        var last = _chars[^1];
        if (last.Base != v)
        {
            // Gõ dấu mũ sau phụ âm cuối / nguyên âm khác: nhapa → nhâp, cuoio → cuôi, tiene → tiên.
            if (!Config.FreeMarking || NucleusIndex(v) is not { } i || _chars[i].Mark != Mark.None) return false;
            SetMark(i, Mark.Circumflex);
            // Chỉ khi ra vần hợp lệ: ngoeo (ngoèo) không thành "ngôe".
            if (!SpellChecker.IsValid(_chars, Tone.None, Config.AllowZFWJ))
            {
                SetMark(i, Mark.None);
                return false;
            }
            return true;
        }
        switch (last.Mark)
        {
            case Mark.Circumflex:
                SetMark(_chars.Count - 1, Mark.None);
                return Undo(key);
            case Mark.None:
                SetMark(_chars.Count - 1, Mark.Circumflex);
                return true;
            default:
                return false;
        }
    }

    private bool ApplyDoubleD(char key)
    {
        if (_chars.Count == 0 || !SpellGate()) return false;
        var last = _chars[^1];
        if (last.Base != 'd')
        {
            // Gõ d thứ hai sau nguyên âm: dud → đu, dudowcj → được.
            if (!Config.FreeMarking || _chars.Count < 2 || _chars[0].Base != 'd' || _chars[0].Mark != Mark.None
                || NucleusRange() is null) return false;
            SetMark(0, Mark.Stroke);
            return true;
        }
        if (last.Mark == Mark.Stroke)
        {
            SetMark(_chars.Count - 1, Mark.None);
            return Undo(key);
        }
        if (last.Mark != Mark.None) return false;
        SetMark(_chars.Count - 1, Mark.Stroke);
        return true;
    }

    private bool ApplyStroke(char key)
    {
        if (!SpellGate()) return false;
        int? idx;
        if (Config.FreeMarking) idx = _chars.Count > 0 && _chars[0].Base == 'd' ? 0 : null;
        else idx = _chars.Count > 0 && _chars[^1].Base == 'd' ? _chars.Count - 1 : null;
        if (idx is not { } i) return false;
        if (_chars[i].Mark == Mark.Stroke)
        {
            SetMark(i, Mark.None);
            return Undo(key);
        }
        SetMark(i, Mark.Stroke);
        return true;
    }

    private bool ApplyCircumflex(char key)
    {
        if (!SpellGate() || TargetIndex('a', 'e', 'o') is not { } i) return false;
        if (_chars[i].Mark == Mark.Circumflex)
        {
            SetMark(i, Mark.None);
            return Undo(key);
        }
        SetMark(i, Mark.Circumflex);
        return true;
    }

    private enum HornKind { W, Horn, Breve }

    private bool ApplyHornBreve(HornKind kind, char key)
    {
        if (!SpellGate()) return false;

        if (kind == HornKind.W && _chars.Count > 0 && _chars[^1].FromW)
        {
            _chars.RemoveAt(_chars.Count - 1);
            return Undo(key);
        }

        if (kind != HornKind.Breve && UoPairIndex() is { } p)
        {
            var u = _chars[p];
            var o = _chars[p + 1];
            if (u.Mark == Mark.Horn && o.Mark == Mark.Horn)
            {
                SetMark(p, Mark.None);
                SetMark(p + 1, Mark.None);
                return Undo(key);
            }
            if (o.Mark != Mark.Circumflex)
            {
                SetMark(p, Mark.Horn);
                SetMark(p + 1, Mark.Horn);
                return true;
            }
        }

        // Cụm "ua" (không sau q): w móc vào u → mưa, nữa, cửa (không phải "uă").
        if (kind != HornKind.Breve && NucleusRange() is { } r && r.End - r.Start == 2
            && _chars[r.Start].Base == 'u' && _chars[r.Start + 1].Base == 'a'
            && _chars[r.Start + 1].Mark == Mark.None
            && !(r.Start > 0 && _chars[r.Start - 1].Base == 'q'))
        {
            int i = r.Start;
            if (_chars[i].Mark == Mark.Horn)
            {
                SetMark(i, Mark.None);
                return Undo(key);
            }
            SetMark(i, Mark.Horn);
            return true;
        }

        int? target = kind switch
        {
            HornKind.W => TargetIndex('a', 'o', 'u'),
            HornKind.Horn => TargetIndex('o', 'u'),
            _ => TargetIndex('a'),
        };
        if (target is { } t)
        {
            var want = _chars[t].Base == 'a' ? Mark.Breve : Mark.Horn;
            if (_chars[t].Mark == want)
            {
                SetMark(t, Mark.None);
                return Undo(key);
            }
            SetMark(t, want);
            return true;
        }

        if (kind == HornKind.W && Config.WAsU && !_chars.Any(c => c.IsVowel))
        {
            _chars.Add(new VChar('u', Mark.Horn, char.IsUpper(key), FromW: true));
            return true;
        }
        return false;
    }

    private static readonly HashSet<string> ValidFinals = ["", "c", "ch", "m", "n", "ng", "nh", "p", "t"];

    /// <summary>
    /// Cụm nguyên âm duy nhất của từ, chỉ khi phần sau nó là phụ âm cuối hợp lệ (hoặc trống).
    /// Từ có hai cụm nguyên âm (banana, data…) không phải âm tiết tiếng Việt → null.
    /// </summary>
    private (int Start, int End)? NucleusRange()
    {
        int start = _chars.FindIndex(c => c.IsVowel);
        if (start < 0) return null;
        int end = start;
        while (end < _chars.Count && _chars[end].IsVowel) end++;
        var tail = new StringBuilder();
        for (int k = end; k < _chars.Count; k++) tail.Append(_chars[k].Base);
        return ValidFinals.Contains(tail.ToString()) ? (start, end) : null;
    }

    /// <summary>Vị trí nguyên âm <paramref name="v"/> (gần cuối nhất) trong cụm nguyên âm của từ.</summary>
    private int? NucleusIndex(char v)
    {
        if (NucleusRange() is not { } r) return null;
        for (int i = r.End - 1; i >= r.Start; i--)
            if (_chars[i].Base == v) return i;
        return null;
    }

    /// <summary>Chỉ số của <c>u</c> trong cặp <c>uo</c> (không tính <c>qu</c>) để gõ <c>ươ</c>.</summary>
    private int? UoPairIndex()
    {
        for (int i = 0; i + 1 < _chars.Count; i++)
        {
            if (_chars[i].Base != 'u' || _chars[i + 1].Base != 'o') continue;
            if (i > 0 && _chars[i - 1].Base == 'q') continue;
            return i;
        }
        return null;
    }

    /// <summary>Ký tự gần cuối nhất thuộc <paramref name="allowed"/> (u sau q bị bỏ qua). Khi tắt bỏ dấu tự do phải là ký tự cuối.</summary>
    private int? TargetIndex(params char[] allowed)
    {
        for (int i = _chars.Count - 1; i >= 0; i--)
        {
            var c = _chars[i];
            if (Array.IndexOf(allowed, c.Base) < 0) continue;
            if (c.Base == 'u' && i > 0 && _chars[i - 1].Base == 'q') continue;
            if (!Config.FreeMarking && i != _chars.Count - 1) return null;
            return i;
        }
        return null;
    }

    // Dựng kết quả

    /// <summary>
    /// <c>ươ</c> ở cuối từ (chưa có phụ âm cuối) hiển thị là <c>uơ</c> như trong "thuở", "huơ";
    /// khi gõ thêm phụ âm cuối hoặc nguyên âm thì <c>u</c> lại có móc ("thương", "người").
    /// </summary>
    private List<VChar> DisplayChars()
    {
        var output = new List<VChar>(_chars);
        int n = output.Count;
        if (n >= 2 && output[n - 1].Base == 'o' && output[n - 1].Mark == Mark.Horn
            && output[n - 2].Base == 'u' && output[n - 2].Mark == Mark.Horn
            && !(n >= 3 && output[n - 3].Base == 'q'))
        {
            output[n - 2] = output[n - 2] with { Mark = Mark.None };
        }
        return output;
    }

    private string RenderUnicode()
    {
        var display = DisplayChars();
        var position = ToneMarker.Position(display, Config.ModernTone);
        var sb = new StringBuilder(display.Count + 2);
        for (int i = 0; i < display.Count; i++)
            display[i].AppendUnicode(sb, i == position ? _tone : Tone.None);
        return sb.ToString();
    }

    private EngineResult Refresh(char typed)
    {
        var encoded = Config.Charset.Encode(RenderUnicode());
        var (backspaces, text) = Diff(_onScreen, encoded);
        _onScreen = encoded;
        if (backspaces == 0 && text.Length == 1 && text[0] == typed) return EngineResult.Pass;
        return new EngineResult(backspaces, text, false);
    }

    private (int Backspaces, string Text) Diff(string old, string @new)
    {
        var a = TextElements.Split(old);
        var b = TextElements.Split(@new);
        int common = 0;
        while (common < a.Count && common < b.Count && TextElements.Equal(a[common], b[common])) common++;

        int backspaces = 0;
        for (int i = common; i < a.Count; i++)
            backspaces += Config.BackspacePerCodePoint ? TextElements.CodePoints(a[i]) : 1;

        var text = new StringBuilder();
        for (int i = common; i < b.Count; i++) text.Append(b[i]);
        return (backspaces, text.ToString());
    }

    private EngineResult Replace(string text)
    {
        var encoded = Config.Charset.Encode(text);
        var (backspaces, replacement) = Diff(_onScreen, encoded);
        if (backspaces == 0 && replacement.Length == 0) return EngineResult.Pass;
        return new EngineResult(backspaces, replacement, true);
    }

    private string? MacroExpansion() =>
        Macros.IsEmpty ? null : Macros.Expand(new string(_raw.ToArray()), Config.MacroKeepCase);

    private bool ShouldRestore()
    {
        if (new string(_raw.ToArray()) == RenderUnicode()) return false;
        return !SpellChecker.IsValid(DisplayChars(), _tone, Config.AllowZFWJ);
    }

    private void UpdateCapState(char afterBreak)
    {
        if (!Config.AutoCapitalize)
        {
            _capState = CapState.None;
            return;
        }
        switch (afterBreak)
        {
            case '.' or '!' or '?': _capState = CapState.AfterPunct; break;
            case ' ': _capState = _capState is CapState.AfterPunct or CapState.Armed ? CapState.Armed : CapState.None; break;
            case '\n' or '\r': _capState = CapState.Armed; break;
            case '\t': break;
            default: _capState = CapState.None; break;
        }
    }
}
