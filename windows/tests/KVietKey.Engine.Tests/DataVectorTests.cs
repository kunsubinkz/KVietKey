using System.Text.Json;
using KVietKey.Engine;

namespace KVietKey.Engine.Tests;

public class CharsetVectorTests
{
    [Fact]
    public void EncodeDecodeMatchSwift()
    {
        using var doc = Vectors.Load("charset.json");
        var failures = new List<string>();
        int count = 0;
        foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            count++;
            var cs = Enum.Parse<Charset>(row.GetProperty("charset").GetString()!, ignoreCase: true);
            var text = row.GetProperty("text").GetString()!;
            var encoded = row.GetProperty("encoded").GetString()!;
            var decoded = row.GetProperty("decoded").GetString()!;
            if (cs.Encode(text) != encoded) failures.Add($"encode {cs} '{text}'");
            if (cs.Decode(encoded) != decoded) failures.Add($"decode {cs} '{text}'");
        }
        Assert.True(count > 1000);
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(15)));
    }
}

public class MacroVectorTests
{
    [Fact]
    public void ExpandMatchesSwift()
    {
        using var doc = Vectors.Load("macro.json");
        var table = Vectors.ParseMacros(doc.RootElement.GetProperty("entries"));
        var failures = new List<string>();
        foreach (var row in doc.RootElement.GetProperty("expand").EnumerateArray())
        {
            var typed = row.GetProperty("typed").GetString()!;
            bool keep = row.GetProperty("keepCase").GetBoolean();
            string? expected = row.TryGetProperty("expected", out var e) ? e.GetString() : null;
            var got = table.Expand(typed, keep);
            if (got != expected) failures.Add($"typed='{typed}' keep={keep} expected={expected ?? "nil"} got={got ?? "nil"}");
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void ParseAndExportMatchSwift()
    {
        using var doc = Vectors.Load("macro.json");
        foreach (var row in doc.RootElement.GetProperty("parse").EnumerateArray())
        {
            var text = row.GetProperty("text").GetString()!;
            var expected = Vectors.ParseMacros(row.GetProperty("entries"));
            var parsed = MacroTable.Parse(text);
            Assert.Equal(expected, parsed);
            Assert.Equal(row.GetProperty("export").GetString(), parsed.ExportText());
        }
    }

    [Fact]
    public void JsonRoundTripUsesMacOsSchema()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kvk-" + Guid.NewGuid());
        try
        {
            var path = Path.Combine(dir, "macros.json");
            var table = new MacroTable([KeyValuePair.Create("vn", "Việt Nam")]);
            table.Save(path);
            Assert.Equal(table, MacroTable.Load(path));

            // File do bản macOS ghi ra phải đọc được.
            File.WriteAllText(path, "{\n  \"entries\" : {\n    \"dc\" : \"được\"\n  }\n}");
            Assert.Equal("được", MacroTable.Load(path).Expand("dc", true));
            Assert.True(MacroTable.Load(Path.Combine(dir, "missing.json")).IsEmpty);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}

public class SpellVectorTests
{
    [Fact]
    public void IsValidMatchesSwift()
    {
        using var doc = Vectors.Load("spell.json");
        var failures = new List<string>();
        int count = 0;
        foreach (var row in doc.RootElement.GetProperty("rows").EnumerateArray())
        {
            count++;
            var word = row.GetProperty("word").GetString()!;
            var tone = (Tone)row.GetProperty("tone").GetInt32();
            bool foreign = row.GetProperty("foreign").GetBoolean();
            var chars = SpellVectors.VChars(word);
            if (SpellChecker.IsValid(chars, tone, foreign) != row.GetProperty("valid").GetBoolean())
                failures.Add($"isValid {word} tone={tone} foreign={foreign}");
            if (SpellChecker.InitialIsValid(chars, foreign) != row.GetProperty("initialValid").GetBoolean())
                failures.Add($"initialIsValid {word} foreign={foreign}");
        }
        Assert.True(count > 3000, $"chỉ có {count} dòng vector chính tả");
        Assert.True(failures.Count == 0, string.Join("\n", failures.Take(15)));
    }
}

internal static class SpellVectors
{
    /// <summary>Dựng bộ đệm engine từ một từ tiếng Việt (giống <c>vchars</c> trong VectorExportTests.swift).</summary>
    public static List<VChar> VChars(string word)
    {
        var result = new List<VChar>();
        foreach (var element in System.Globalization.StringInfo.GetTextElementEnumerator(word.Normalize(System.Text.NormalizationForm.FormC)).AsEnumerable())
        {
            if (element.ToLowerInvariant() == "đ")
            {
                result.Add(new VChar('d', Mark.Stroke, char.IsUpper(element[0])));
                continue;
            }
            VChar? v = null;
            foreach (var u in element.Normalize(System.Text.NormalizationForm.FormD))
            {
                switch (u)
                {
                    case '\u0302': v = v!.Value with { Mark = Mark.Circumflex }; break;
                    case '\u0306': v = v!.Value with { Mark = Mark.Breve }; break;
                    case '\u031B': v = v!.Value with { Mark = Mark.Horn }; break;
                    case '\u0301' or '\u0300' or '\u0309' or '\u0303' or '\u0323': break;
                    default: v = new VChar(char.ToLowerInvariant(u), Upper: char.IsUpper(u)); break;
                }
            }
            if (v is { } vc) result.Add(vc);
        }
        return result;
    }

    public static IEnumerable<string> AsEnumerable(this System.Globalization.TextElementEnumerator e)
    {
        while (e.MoveNext()) yield return (string)e.Current;
    }
}
