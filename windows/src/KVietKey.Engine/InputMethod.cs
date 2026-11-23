namespace KVietKey.Engine;

internal enum ActionKind { Tone, ClearTone, DoubleVowel, DoubleD, W, Circumflex, Horn, Breve, Stroke }

/// <summary>Hành động mà một phím gõ có thể kích hoạt.</summary>
internal readonly record struct KeyAction(ActionKind Kind, Tone Tone = Tone.None, char Vowel = '\0')
{
    public static KeyAction OfTone(Tone t) => new(ActionKind.Tone, t);
    public static KeyAction Double(char v) => new(ActionKind.DoubleVowel, Vowel: v);
}

public enum InputMethod { Telex, Vni, Viqr, Combined }

public static class InputMethodExtensions
{
    public static string DisplayName(this InputMethod method) => method switch
    {
        InputMethod.Telex => "Telex",
        InputMethod.Vni => "VNI",
        InputMethod.Viqr => "VIQR",
        InputMethod.Combined => "Telex + VNI",
        _ => method.ToString(),
    };

    /// <summary>Chuyển tên viết thường dùng trong JSON (telex, vni, viqr, combined) thành giá trị enum.</summary>
    public static bool TryParse(string? name, out InputMethod method) =>
        Enum.TryParse(name, ignoreCase: true, out method) && Enum.IsDefined(method);

    internal static KeyAction? ActionFor(this InputMethod method, char key) => method switch
    {
        InputMethod.Telex => Telex(key),
        InputMethod.Vni => Vni(key),
        InputMethod.Viqr => Viqr(key),
        InputMethod.Combined => Telex(key) ?? Vni(key),
        _ => null,
    };

    private static KeyAction? Telex(char key) => key switch
    {
        's' => KeyAction.OfTone(Tone.Acute),
        'f' => KeyAction.OfTone(Tone.Grave),
        'r' => KeyAction.OfTone(Tone.Hook),
        'x' => KeyAction.OfTone(Tone.Tilde),
        'j' => KeyAction.OfTone(Tone.Dot),
        'z' => new KeyAction(ActionKind.ClearTone),
        'a' or 'e' or 'o' => KeyAction.Double(key),
        'd' => new KeyAction(ActionKind.DoubleD),
        'w' => new KeyAction(ActionKind.W),
        _ => null,
    };

    private static KeyAction? Vni(char key) => key switch
    {
        '1' => KeyAction.OfTone(Tone.Acute),
        '2' => KeyAction.OfTone(Tone.Grave),
        '3' => KeyAction.OfTone(Tone.Hook),
        '4' => KeyAction.OfTone(Tone.Tilde),
        '5' => KeyAction.OfTone(Tone.Dot),
        '0' => new KeyAction(ActionKind.ClearTone),
        '6' => new KeyAction(ActionKind.Circumflex),
        '7' => new KeyAction(ActionKind.Horn),
        '8' => new KeyAction(ActionKind.Breve),
        '9' => new KeyAction(ActionKind.Stroke),
        _ => null,
    };

    private static KeyAction? Viqr(char key) => key switch
    {
        '\'' => KeyAction.OfTone(Tone.Acute),
        '`' => KeyAction.OfTone(Tone.Grave),
        '?' => KeyAction.OfTone(Tone.Hook),
        '~' => KeyAction.OfTone(Tone.Tilde),
        '.' => KeyAction.OfTone(Tone.Dot),
        '-' => new KeyAction(ActionKind.ClearTone),
        '^' => new KeyAction(ActionKind.Circumflex),
        '(' => new KeyAction(ActionKind.Breve),
        '+' => new KeyAction(ActionKind.Horn),
        'd' => new KeyAction(ActionKind.DoubleD),
        _ => null,
    };
}
