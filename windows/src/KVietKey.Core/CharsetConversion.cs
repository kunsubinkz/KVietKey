using KVietKey.Engine;

namespace KVietKey.Core;

/// <summary>Chuyển mã văn bản (clipboard): TCVN3/VNI/VISCII/CP1258 ⇄ Unicode, tổ hợp ⇄ dựng sẵn.</summary>
public sealed record CharsetConversion(string Title, Charset From, Charset To)
{
    public string Apply(string text) => To.Encode(From.Decode(text));

    public static IReadOnlyList<CharsetConversion> All { get; } = Build();

    private static List<CharsetConversion> Build()
    {
        var list = new List<CharsetConversion>();
        foreach (var cs in new[] { Charset.Tcvn3, Charset.VniWindows, Charset.Viscii, Charset.Cp1258 })
        {
            list.Add(new($"{cs.DisplayName()} → Unicode", cs, Charset.Unicode));
            list.Add(new($"Unicode → {cs.DisplayName()}", Charset.Unicode, cs));
        }
        list.Add(new("Unicode tổ hợp → Unicode dựng sẵn", Charset.UnicodeComposite, Charset.Unicode));
        list.Add(new("Unicode dựng sẵn → Unicode tổ hợp", Charset.Unicode, Charset.UnicodeComposite));
        return list;
    }
}
