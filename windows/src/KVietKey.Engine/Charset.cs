using System.Text;

namespace KVietKey.Engine;

/// <summary>
/// Bảng mã đầu ra. Engine luôn dựng từ bằng Unicode dựng sẵn (NFC) rồi chuyển sang bảng mã đích.
/// Với các bảng mã cũ (TCVN3, VNI Windows, VISCII, CP1258) mỗi byte được xuất ra thành ký tự
/// có điểm mã bằng giá trị byte — dùng với phông chữ tương ứng, giống các bộ gõ khác.
/// </summary>
public enum Charset { Unicode, UnicodeComposite, Tcvn3, VniWindows, Viscii, Cp1258 }

public static class CharsetExtensions
{
    public static string DisplayName(this Charset cs) => cs switch
    {
        Charset.Unicode => "Unicode dựng sẵn",
        Charset.UnicodeComposite => "Unicode tổ hợp",
        Charset.Tcvn3 => "TCVN3 (ABC)",
        Charset.VniWindows => "VNI Windows",
        Charset.Viscii => "VISCII",
        Charset.Cp1258 => "CP1258",
        _ => cs.ToString(),
    };

    /// <summary>Mỗi ký tự tiếng Việt chiếm đúng một lần Backspace trên màn hình.</summary>
    internal static bool SingleBackspacePerLetter(this Charset cs) => cs switch
    {
        Charset.VniWindows or Charset.Cp1258 => false,
        _ => true,
    };

    private sealed class Maps
    {
        public readonly Dictionary<char, string> Encode = [];
        public readonly Dictionary<string, string> Decode = new(StringComparer.Ordinal);
        public int MaxKeyLength = 1;
    }

    private static (ushort Code, byte[] Bytes)[]? Table(Charset cs) => cs switch
    {
        Charset.Tcvn3 => CharsetTables.Tcvn3,
        Charset.VniWindows => CharsetTables.VniWindows,
        Charset.Viscii => CharsetTables.Viscii,
        Charset.Cp1258 => CharsetTables.Cp1258,
        _ => null,
    };

    private static readonly Dictionary<Charset, Maps> Cache = BuildCache();

    private static Dictionary<Charset, Maps> BuildCache()
    {
        var result = new Dictionary<Charset, Maps>();
        foreach (var cs in Enum.GetValues<Charset>())
        {
            var table = Table(cs);
            if (table is null) continue;
            var maps = new Maps();
            foreach (var (code, bytes) in table)
            {
                var ch = (char)code;
                var legacy = string.Create(bytes.Length, bytes, static (span, b) =>
                {
                    for (int i = 0; i < b.Length; i++) span[i] = (char)b[i];
                });
                maps.Encode[ch] = legacy;
                // Bảng mã chữ hoa/thường dùng chung mã: ưu tiên chữ thường khi giải mã.
                if (maps.Decode.TryGetValue(legacy, out var existing))
                {
                    if (!char.IsLower(existing[0])) maps.Decode[legacy] = ch.ToString();
                }
                else
                {
                    maps.Decode[legacy] = ch.ToString();
                }
                maps.MaxKeyLength = Math.Max(maps.MaxKeyLength, bytes.Length);
            }
            result[cs] = maps;
        }
        return result;
    }

    /// <summary>Chuyển văn bản Unicode dựng sẵn sang bảng mã này.</summary>
    public static string Encode(this Charset cs, string text)
    {
        switch (cs)
        {
            case Charset.Unicode:
                return text;
            case Charset.UnicodeComposite:
                return text.Normalize(NormalizationForm.FormD);
            default:
                if (!Cache.TryGetValue(cs, out var maps)) return text;
                var sb = new StringBuilder(text.Length + 8);
                foreach (var ch in text.Normalize(NormalizationForm.FormC))
                {
                    if (maps.Encode.TryGetValue(ch, out var legacy)) sb.Append(legacy);
                    else sb.Append(ch);
                }
                return sb.ToString();
        }
    }

    /// <summary>Chuyển văn bản đang ở bảng mã này về Unicode dựng sẵn.</summary>
    public static string Decode(this Charset cs, string text)
    {
        if (cs is Charset.Unicode or Charset.UnicodeComposite) return text.Normalize(NormalizationForm.FormC);
        if (!Cache.TryGetValue(cs, out var maps)) return text;

        var sb = new StringBuilder(text.Length);
        int i = 0;
        while (i < text.Length)
        {
            bool matched = false;
            for (int len = Math.Min(maps.MaxKeyLength, text.Length - i); len >= 1; len--)
            {
                if (maps.Decode.TryGetValue(text.Substring(i, len), out var ch))
                {
                    sb.Append(ch);
                    i += len;
                    matched = true;
                    break;
                }
            }
            if (!matched) sb.Append(text[i++]);
        }
        return sb.ToString();
    }
}
