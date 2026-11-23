using System.Text;

namespace KVietKey.Engine;

/// <summary>Dấu thanh: sắc, huyền, hỏi, ngã, nặng.</summary>
public enum Tone { None, Acute, Grave, Hook, Tilde, Dot }

internal static class ToneExtensions
{
    public static char? Combining(this Tone tone) => tone switch
    {
        Tone.Acute => '\u0301',
        Tone.Grave => '\u0300',
        Tone.Hook => '\u0309',
        Tone.Tilde => '\u0303',
        Tone.Dot => '\u0323',
        _ => null,
    };
}

/// <summary>Dấu phụ của chữ cái: mũ (â ê ô), trăng (ă), móc (ơ ư), gạch (đ).</summary>
internal enum Mark { None, Circumflex, Breve, Horn, Stroke }

internal static class MarkExtensions
{
    public static char? Combining(this Mark mark) => mark switch
    {
        Mark.Circumflex => '\u0302',
        Mark.Breve => '\u0306',
        Mark.Horn => '\u031B',
        _ => null,
    };
}

/// <summary>Một ký tự trong bộ đệm từ hiện tại. <c>Base</c> luôn là chữ thường.</summary>
internal readonly record struct VChar(char Base, Mark Mark = Mark.None, bool Upper = false, bool FromW = false)
{
    public bool IsVowel => Base is 'a' or 'e' or 'i' or 'o' or 'u' or 'y';

    /// <summary>Nhãn chữ thường không dấu thanh, dùng cho kiểm tra chính tả (vd. "ê", "đ").</summary>
    public char Label => (Base, Mark) switch
    {
        ('a', Mark.Circumflex) => 'â',
        ('a', Mark.Breve) => 'ă',
        ('e', Mark.Circumflex) => 'ê',
        ('o', Mark.Circumflex) => 'ô',
        ('o', Mark.Horn) => 'ơ',
        ('u', Mark.Horn) => 'ư',
        ('d', Mark.Stroke) => 'đ',
        _ => Base,
    };

    public void AppendUnicode(StringBuilder sb, Tone tone)
    {
        if (Base == 'd' && Mark == Mark.Stroke)
        {
            sb.Append(Upper ? 'Đ' : 'đ');
            return;
        }
        var mark = Mark.Combining();
        var tn = tone.Combining();
        if (mark is null && tn is null)
        {
            sb.Append(Upper ? char.ToUpperInvariant(Base) : Base);
            return;
        }
        Span<char> buffer = stackalloc char[3];
        int n = 0;
        buffer[n++] = Base;
        if (mark is { } m) buffer[n++] = m;
        if (tn is { } t) buffer[n++] = t;
        var composed = new string(buffer[..n]).Normalize(NormalizationForm.FormC);
        sb.Append(Upper ? composed.ToUpperInvariant() : composed);
    }
}

/// <summary>Kết quả xử lý một phím.</summary>
public readonly record struct EngineResult(int Backspaces, string Text, bool Passthrough)
{
    /// <summary>Phím gốc vẫn phải được chuyển tiếp tới ứng dụng, không có thay thế.</summary>
    public static readonly EngineResult Pass = new(0, "", true);

    public bool IsEmpty => Backspaces == 0 && Text.Length == 0;
}
