using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KVietKey.Engine;

/// <summary>Bảng gõ tắt: <c>viết_tắt</c> → <c>nội dung</c>.</summary>
public sealed class MacroTable : IEquatable<MacroTable>
{
    [JsonPropertyName("entries")]
    public Dictionary<string, string> Entries { get; set; } = new(StringComparer.Ordinal);

    public MacroTable() { }

    public MacroTable(IEnumerable<KeyValuePair<string, string>> entries)
    {
        foreach (var (k, v) in entries) Entries[k] = v;
    }

    [JsonIgnore]
    public bool IsEmpty => Entries.Count == 0;

    /// <summary>
    /// Tìm nội dung mở rộng cho <paramref name="typed"/> (chuỗi phím đã gõ).
    /// Khi <paramref name="keepCase"/> bật: <c>VN</c> → toàn bộ hoa, <c>Vn</c> → viết hoa chữ đầu.
    /// </summary>
    public string? Expand(string typed, bool keepCase)
    {
        if (Entries.TryGetValue(typed, out var exact)) return exact;
        if (!keepCase) return null;
        if (!Entries.TryGetValue(typed.ToLowerInvariant(), out var value)) return null;

        int letters = 0;
        bool allUpper = true;
        foreach (var ch in typed)
        {
            if (!char.IsLetter(ch)) continue;
            letters++;
            if (!char.IsUpper(ch)) allUpper = false;
        }
        if (letters >= 2 && allUpper) return value.ToUpperInvariant();
        if (typed.Length > 0 && char.IsUpper(typed[0]) && value.Length > 0)
        {
            int first = StringInfo.GetNextTextElementLength(value);
            return value[..first].ToUpperInvariant() + value[first..];
        }
        return value;
    }

    // Nhập / xuất .txt: mỗi dòng `viết_tắt:nội dung`.

    private static readonly char[] LineBreaks = ['\n', '\r', '\v', '\f', '\u0085', '\u2028', '\u2029'];

    public static MacroTable Parse(string text)
    {
        var table = new MacroTable();
        foreach (var line in text.Split(LineBreaks, StringSplitOptions.RemoveEmptyEntries))
        {
            int idx = line.IndexOf(':');
            if (idx < 0) continue;
            var key = line[..idx].Trim();
            var value = line[(idx + 1)..].Trim();
            if (key.Length > 0 && value.Length > 0) table.Entries[key] = value;
        }
        return table;
    }

    public string ExportText()
    {
        var keys = Entries.Keys.ToList();
        keys.Sort(StringComparer.Ordinal);
        // Giống bản macOS: nối bằng "\n" rồi thêm "\n" cuối (bảng rỗng cho ra "\n").
        return string.Join("\n", keys.Select(k => $"{k}:{Entries[k]}")) + "\n";
    }

    public void Merge(MacroTable other)
    {
        foreach (var (k, v) in other.Entries) Entries[k] = v;
    }

    public MacroTable Clone() => new(Entries);

    // Lưu trữ JSON: {"entries": {...}} giống bản macOS.

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static MacroTable Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var table = JsonSerializer.Deserialize<MacroTable>(stream, JsonOptions);
            if (table is null) return new MacroTable();
            table.Entries = new Dictionary<string, string>(table.Entries, StringComparer.Ordinal);
            return table;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new MacroTable();
        }
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var sorted = new MacroTable(Entries.OrderBy(e => e.Key, StringComparer.Ordinal));
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(sorted, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    public bool Equals(MacroTable? other) =>
        other is not null && Entries.Count == other.Entries.Count
        && Entries.All(e => other.Entries.TryGetValue(e.Key, out var v) && v == e.Value);

    public override bool Equals(object? obj) => Equals(obj as MacroTable);
    public override int GetHashCode() => Entries.Count;
}
