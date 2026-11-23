using KVietKey.Core;
using KVietKey.Engine;

namespace KVietKey.Core.Tests;

public class SettingsTests
{
    [Fact]
    public void JsonKeysMatchMacOsSchema()
    {
        var json = new Settings().ToJson();
        foreach (var key in new[]
        {
            "method", "charset", "modernTone", "freeMarking", "spellCheck", "autoRestore", "autoCapitalize",
            "allowZFWJ", "wAsU", "macroEnabled", "macroKeepCase", "isVietnamese", "hotkey", "launchAtLogin",
            "soundOnToggle", "rememberAppState", "appRules", "lastAppState", "disableInFullscreen",
            "browserAutocompleteFix", "electronDelayMicros", "language", "debugLog", "onboardingDone",
        })
        {
            Assert.Contains($"\"{key}\"", json);
        }
        Assert.Contains("\"method\": \"telex\"", json);
        Assert.Contains("\"charset\": \"unicode\"", json);
    }

    [Fact]
    public void RoundTripKeepsEverything()
    {
        var s = new Settings
        {
            Method = InputMethod.Vni,
            Charset = Charset.VniWindows,
            Hotkey = Hotkey.CtrlSpace,
            Language = AppLanguage.En,
            AutoCapitalize = true,
        };
        s.AppRules["chrome.exe"] = new AppRule { Mode = AppMode.English, Charset = Charset.Tcvn3 };
        s.LastAppState["code.exe"] = false;

        var back = Settings.FromJson(s.ToJson());
        Assert.True(s.SameAs(back));
        Assert.Equal(Charset.Tcvn3, back.AppRules["CHROME.EXE"].Charset);
        Assert.False(back.LastAppState["code.exe"]);
    }

    [Fact]
    public void MissingAndInvalidKeysFallBackIndividually()
    {
        var s = Settings.FromJson("""
            { "method": "viqr", "charset": "nonsense", "modernTone": "yes", "electronDelayMicros": 900,
              "language": "zz", "unknownKey": 1, "spellCheck": false }
            """);
        Assert.Equal(InputMethod.Viqr, s.Method);
        Assert.Equal(Charset.Unicode, s.Charset);
        Assert.True(s.ModernTone);
        Assert.Equal(900, s.ElectronDelayMicros);
        Assert.Equal(AppLanguage.Auto, s.Language);
        Assert.False(s.SpellCheck);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    public void BrokenFileGivesDefaults(string text)
    {
        Assert.True(Settings.FromJson(text).SameAs(new Settings()));
    }

    [Fact]
    public void MacOsHotkeyBitsAreIgnored()
    {
        // Bản macOS lưu cờ CGEventFlags (vd. Control|Shift = 0x60000); không dùng được trên Windows.
        var s = Settings.FromJson("""{ "hotkey": { "modifiers": 393216 }, "method": "vni" }""");
        Assert.Equal(Hotkey.CtrlShift, s.Hotkey);
        Assert.Equal(InputMethod.Vni, s.Method);
    }

    [Fact]
    public void EngineConfigUsesCodePointBackspaceOnlyForComposite()
    {
        Assert.False(new Settings { Charset = Charset.Unicode }.ToEngineConfig().BackspacePerCodePoint);
        Assert.True(new Settings { Charset = Charset.UnicodeComposite }.ToEngineConfig().BackspacePerCodePoint);
        Assert.True(new Settings().ToEngineConfig(Charset.UnicodeComposite).BackspacePerCodePoint);
    }

    [Fact]
    public void HotkeyDisplay()
    {
        Assert.Equal("Ctrl + Shift", Hotkey.CtrlShift.DisplayString);
        Assert.Equal("Ctrl + Space", Hotkey.CtrlSpace.DisplayString);
        Assert.Equal("Alt + Shift", Hotkey.AltShift.DisplayString);
    }
}

public class PreferencesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "kvk-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void UpdatePersistsAndRaisesEventOnlyOnChange()
    {
        var prefs = new Preferences(_dir);
        int events = 0;
        prefs.SettingsChanged += (_, _) => events++;

        prefs.Update(s => s.Method = InputMethod.Vni);
        prefs.Update(s => s.Method = InputMethod.Vni);
        Assert.Equal(1, events);

        var reloaded = new Preferences(_dir);
        Assert.Equal(InputMethod.Vni, reloaded.Current.Method);
    }

    [Fact]
    public void PublishedSnapshotsAreNeverMutated()
    {
        var prefs = new Preferences(_dir);
        var before = prefs.Current;
        prefs.Update(s => s.SpellCheck = false);
        Assert.True(before.SpellCheck);
        Assert.False(prefs.Current.SpellCheck);
    }

    [Fact]
    public void MacrosPersistSeparately()
    {
        var prefs = new Preferences(_dir);
        int events = 0;
        prefs.MacrosChanged += () => events++;
        var table = new MacroTable([KeyValuePair.Create("vn", "Việt Nam")]);
        prefs.SetMacros(table);
        prefs.SetMacros(table);
        Assert.Equal(1, events);
        Assert.Equal("Việt Nam", new Preferences(_dir).Macros.Expand("vn", true));
        Assert.True(File.Exists(Path.Combine(_dir, "macros.json")));
    }

    [Fact]
    public void ReadOnlyDirectoryDoesNotThrow()
    {
        var file = Path.Combine(Path.GetTempPath(), "kvk-file-" + Guid.NewGuid());
        File.WriteAllText(file, "x");
        try
        {
            var prefs = new Preferences(Path.Combine(file, "sub")); // thư mục cha là file → không ghi được
            prefs.Update(s => s.SpellCheck = false);
            Assert.False(prefs.Current.SpellCheck);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void PortableMarkerSwitchesDirectory()
    {
        var app = Path.Combine(_dir, "app");
        Directory.CreateDirectory(app);
        Assert.DoesNotContain("data", Preferences.DefaultDirectory(app));
        File.WriteAllText(Path.Combine(app, "portable.txt"), "");
        Assert.Equal(Path.Combine(app, "data"), Preferences.DefaultDirectory(app));
    }
}
