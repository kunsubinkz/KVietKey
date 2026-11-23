using KVietKey.Engine;

namespace KVietKey.Engine.Tests;

/// <summary>Các hành vi riêng của bản Windows (không có trong bộ vector Swift).</summary>
public class WindowsBehaviorTests
{
    private static EngineConfig Composite(bool perCodePoint) =>
        new() { Charset = Charset.UnicodeComposite, BackspacePerCodePoint = perCodePoint };

    [Theory]
    [InlineData("tieengs vieetj", "tiếng việt")]
    [InlineData("nguwowif dduwowcj", "người được")]
    [InlineData("hoaf thuys", "hoà thuý")]
    [InlineData("Vieetj Nam", "Việt Nam")]
    public void CompositeWithCodePointBackspaceMatchesGraphemeResult(string keys, string expected)
    {
        var grapheme = new Typist(Composite(false));
        var codePoint = new Typist(Composite(true));
        grapheme.Type(keys);
        codePoint.Type(keys);

        Assert.Equal(expected.Normalize(System.Text.NormalizationForm.FormD), grapheme.Screen);
        Assert.Equal(grapheme.Screen, codePoint.Screen);
        Assert.False(codePoint.InvalidBackspace);
    }

    [Fact]
    public void CodePointBackspaceCountsEachCombiningMark()
    {
        var grapheme = new Engine(Composite(false));
        var codePoint = new Engine(Composite(true));
        foreach (var e in new[] { grapheme, codePoint }) foreach (var c in "tiee") e.Process(c);

        // tiê (ê = e + U+0302) + dấu sắc: gõ lại chữ ê thành ế. Xóa 1 cụm ký tự = 2 code point.
        var g = grapheme.Process('s');
        var c2 = codePoint.Process('s');
        Assert.Equal(1, g.Backspaces);
        Assert.Equal(2, c2.Backspaces);
        Assert.Equal(g.Text, c2.Text);
    }

    [Fact]
    public void ToneChangeOnComposedLetterBacksUpCodePoints()
    {
        // "á" (a + U+0301) → gõ thêm 'f' đổi thành "à": cần xóa 1 grapheme / 2 code point.
        var grapheme = new Engine(Composite(false));
        var codePoint = new Engine(Composite(true));
        foreach (var e in new[] { grapheme, codePoint }) { e.Process('a'); e.Process('s'); }
        Assert.Equal(1, grapheme.Process('f').Backspaces);
        Assert.Equal(2, codePoint.Process('f').Backspaces);
    }

    [Fact]
    public void UserBackspaceOnComposedLetterStopsTrackingWord()
    {
        var t = new Typist(Composite(true));
        t.Type("tiee");
        Assert.True(t.Engine.HasPendingWord);
        t.Type("\b"); // chỉ mất dấu mũ trên màn hình
        Assert.Equal("tie", t.Screen);
        Assert.False(t.Engine.HasPendingWord);
        t.Type("t");
        Assert.Equal("tiet", t.Screen);
        Assert.False(t.InvalidBackspace);
    }

    [Fact]
    public void LegacyCharsetsNeverProduceInvalidBackspaceInCodePointMode()
    {
        foreach (var cs in Enum.GetValues<Charset>())
        {
            var t = new Typist(new EngineConfig { Charset = cs, BackspacePerCodePoint = true });
            t.Type("tieengs vieetj nuwowcs dduwowcj hoaf ");
            Assert.False(t.InvalidBackspace, cs.ToString());
        }
    }

    [Fact]
    public void SupplementaryPlaneBreakCharacterDoesNotCrash()
    {
        var t = new Typist();
        t.Type("xin 😀 chaof ");
        Assert.Equal("xin 😀 chào ", t.Screen);
    }

    [Fact]
    public void ProcessingIsFast()
    {
        var engine = new Engine();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int words = 20000;
        for (int i = 0; i < words; i++)
        {
            foreach (var c in "nguwowif ") engine.Process(c);
        }
        double microsPerKey = sw.Elapsed.TotalMilliseconds * 1000 / (words * 9);
        Assert.True(microsPerKey < 50, $"{microsPerKey:F1} µs/phím");
    }
}
