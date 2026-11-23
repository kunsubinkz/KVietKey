using System.Text.Json;
using KVietKey.Engine;

namespace KVietKey.Engine.Tests;

/// <summary>Đọc test vector JSON do bộ test Swift xuất ra (windows/tests/vectors).</summary>
internal static class Vectors
{
    public static string Dir => Path.Combine(AppContext.BaseDirectory, "vectors");

    public static JsonDocument Load(string file) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Dir, file)));

    public static IEnumerable<string> EngineFiles() =>
        Directory.GetFiles(Dir, "engine.*.json").Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal)!;

    public static EngineConfig ParseConfig(JsonElement c)
    {
        static T E<T>(JsonElement c, string name) where T : struct, Enum =>
            Enum.Parse<T>(c.GetProperty(name).GetString()!, ignoreCase: true);

        return new EngineConfig
        {
            Method = E<InputMethod>(c, "method"),
            Charset = E<Charset>(c, "charset"),
            ModernTone = c.GetProperty("modernTone").GetBoolean(),
            FreeMarking = c.GetProperty("freeMarking").GetBoolean(),
            SpellCheck = c.GetProperty("spellCheck").GetBoolean(),
            AutoRestore = c.GetProperty("autoRestore").GetBoolean(),
            AutoCapitalize = c.GetProperty("autoCapitalize").GetBoolean(),
            AllowZFWJ = c.GetProperty("allowZFWJ").GetBoolean(),
            WAsU = c.GetProperty("wAsU").GetBoolean(),
            MacroEnabled = c.GetProperty("macroEnabled").GetBoolean(),
            MacroKeepCase = c.GetProperty("macroKeepCase").GetBoolean(),
        };
    }

    public static MacroTable ParseMacros(JsonElement m) =>
        new(m.EnumerateObject().Select(p => KeyValuePair.Create(p.Name, p.Value.GetString()!)));

    /// <summary>Tên hiển thị nhóm: file | test | #nhóm.</summary>
    public static IEnumerable<object[]> EngineGroups()
    {
        foreach (var file in EngineFiles())
        {
            using var doc = Load(file);
            foreach (var test in doc.RootElement.GetProperty("tests").EnumerateArray())
            {
                int index = 0;
                foreach (var _ in test.GetProperty("groups").EnumerateArray())
                    yield return [file, test.GetProperty("name").GetString()!, index++];
            }
        }
    }
}

public class EngineVectorTests
{
    public static IEnumerable<object[]> Groups() => Vectors.EngineGroups();

    [Theory]
    [MemberData(nameof(Groups))]
    public void MatchesSwiftEngine(string file, string test, int group)
    {
        using var doc = Vectors.Load(file);
        var tests = doc.RootElement.GetProperty("tests").EnumerateArray();
        var g = tests.First(t => t.GetProperty("name").GetString() == test)
                     .GetProperty("groups").EnumerateArray().ElementAt(group);
        var config = Vectors.ParseConfig(g.GetProperty("config"));
        var macros = Vectors.ParseMacros(g.GetProperty("macros"));

        var failures = new List<string>();
        foreach (var c in g.GetProperty("cases").EnumerateArray())
        {
            var input = c.GetProperty("input").GetString()!;
            var expected = c.GetProperty("expected").GetString()!;
            var typist = new Typist(config, macros);
            typist.Type(input);
            if (typist.Screen != expected || typist.InvalidBackspace)
            {
                string Show(string s) => input.Length > 80 ? $"({s.Length} ký tự)" : s;
                failures.Add($"input={Show(input)} expected={Show(expected)} got={Show(typist.Screen)} invalidBackspace={typist.InvalidBackspace}");
            }
        }
        Assert.True(failures.Count == 0,
            $"{failures.Count} ca lệch ({file} / {test}):\n" + string.Join("\n", failures.Take(15)));
    }

    [Fact]
    public void VectorsArePresent()
    {
        Assert.True(Vectors.EngineGroups().Count() >= 20, "Thiếu test vector: chạy KVK_EXPORT_VECTORS=… swift test bên macos/");
    }
}
