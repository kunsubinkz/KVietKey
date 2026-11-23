using System.Globalization;
using System.Text;
using KVietKey.Engine;

namespace KVietKey.Engine.Tests;

/// <summary>Mô phỏng ô văn bản: áp dụng kết quả của engine lên chuỗi màn hình (cùng logic <c>Typist</c> của bản Swift).</summary>
public sealed class Typist
{
    public Engine Engine { get; }
    public string Screen { get; private set; } = "";
    public bool InvalidBackspace { get; private set; }

    public Typist(EngineConfig? config = null, MacroTable? macros = null)
    {
        Engine = new Engine(config ?? new EngineConfig());
        if (macros is not null) Engine.Macros = macros;
    }

    private bool CodePointMode => Engine.Config.BackspacePerCodePoint;

    private int UnitCount() => CodePointMode ? TextElementsCount.CodePoints(Screen) : StringInfo.ParseCombiningCharacters(Screen).Length;

    private void RemoveLast(int n)
    {
        if (n <= 0) return;
        if (CodePointMode)
        {
            var runes = Screen.EnumerateRunes().ToList();
            runes.RemoveRange(runes.Count - Math.Min(n, runes.Count), Math.Min(n, runes.Count));
            Screen = string.Concat(runes.Select(r => r.ToString()));
            return;
        }
        var starts = StringInfo.ParseCombiningCharacters(Screen);
        int keep = Math.Max(0, starts.Length - n);
        Screen = keep == 0 ? "" : Screen[..starts[keep]];
    }

    private void Apply(EngineResult r, Rune original)
    {
        if (r.Backspaces > UnitCount()) InvalidBackspace = true;
        RemoveLast(r.Backspaces);
        Screen += r.Text;
        if (r.Passthrough) Screen += original.ToString();
    }

    public void Type(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == 8)
            {
                Engine.Backspace();
                RemoveLast(1);
            }
            else if (rune.IsBmp && ((char.IsAsciiLetter((char)rune.Value))
                     || (Engine.Config.Method.ActionFor((char)rune.Value) != null && !Rune.IsWhiteSpace(rune))))
            {
                Apply(Engine.Process(rune), rune);
            }
            else
            {
                Apply(Engine.ProcessBreak(rune), rune);
            }
        }
    }

    public static string Run(string text, Func<EngineConfig, EngineConfig>? configure = null)
    {
        var t = new Typist(configure?.Invoke(new EngineConfig()) ?? new EngineConfig());
        t.Type(text);
        return t.Screen;
    }
}

internal static class TextElementsCount
{
    public static int CodePoints(string s)
    {
        int n = 0;
        foreach (var _ in s.EnumerateRunes()) n++;
        return n;
    }
}
