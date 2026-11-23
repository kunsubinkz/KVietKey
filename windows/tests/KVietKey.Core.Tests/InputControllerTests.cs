using System.Text;
using KVietKey.Core;
using KVietKey.Engine;
using KVietKey.Platform.Fake;

namespace KVietKey.Core.Tests;

public sealed class Rig : IDisposable
{
    public readonly string Dir = Path.Combine(Path.GetTempPath(), "kvk-" + Guid.NewGuid());
    public readonly Preferences Prefs;
    public readonly FakeInputSender Screen = new();
    public readonly FakeForegroundMonitor Foreground = new();
    public readonly FakeSystemServices System = new();
    public readonly InputController Controller;
    public readonly VirtualKeyboard Keyboard;

    public Rig(Action<Settings>? configure = null)
    {
        Prefs = new Preferences(Dir);
        if (configure is not null) Prefs.Update(configure);
        Controller = new InputController(Prefs, Screen, Foreground, System);
        Keyboard = new VirtualKeyboard(Controller, Screen);
    }

    public string Type(string text)
    {
        Keyboard.Type(text);
        return Screen.Text;
    }

    public void Dispose()
    {
        if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
    }
}

public class InputControllerTests
{
    [Theory]
    [InlineData("tieengs vieetj ", "tiếng việt ")]
    [InlineData("Vieetj Nam, xin chaof.\n", "Việt Nam, xin chào.\n")]
    [InlineData("windows class ", "windows class ")]
    [InlineData("nguwowif dduwowcj\t", "người được\t")]
    public void TypesVietnameseThroughTheControllerLikeARealApp(string keys, string expected)
    {
        using var rig = new Rig();
        Assert.Equal(expected, rig.Type(keys));
    }

    [Fact]
    public void ReplacementIsSentBeforeTheOriginalBreakKey()
    {
        using var rig = new Rig();
        // "windows" bị biến đổi khi gõ nên ở dấu cách phải khôi phục: gửi thay thế rồi mới phát lại dấu cách.
        rig.Type("windows");
        rig.Screen.Log.Clear();
        Assert.Equal("windows ", rig.Type(" "));
        var sendIndex = rig.Screen.Log.FindIndex(l => l.StartsWith("send"));
        var repostIndex = rig.Screen.Log.FindIndex(l => l.StartsWith("repost"));
        Assert.True(sendIndex < repostIndex, string.Join(" | ", rig.Screen.Log));
    }

    [Fact]
    public void BackspaceKeepsEngineInSync()
    {
        using var rig = new Rig();
        Assert.Equal("vi", rig.Type("vieetj\b\b"));
        Assert.Equal("tơ", new Rig().Type("toans\b\b\bow"));
    }

    [Fact]
    public void ModifierShortcutsResetWordAndPassThrough()
    {
        using var rig = new Rig();
        rig.Type("vie");
        Assert.Equal(KeyDecision.Pass, rig.Keyboard.Press('A', ModifierKeys.Ctrl, new Rune('a')));
        // Từ đã bị bỏ: gõ tiếp 'e' không biến "vie" thành "viê".
        Assert.DoesNotContain("viê", rig.Type("e"));
    }

    [Fact]
    public void NavigationKeysResetTheWord()
    {
        using var rig = new Rig();
        rig.Type("vie");
        rig.Keyboard.Press(VirtualKeys.Left);
        Assert.Equal("viee", rig.Type("e"));
    }

    [Fact]
    public void MouseClickResetsTheWord()
    {
        using var rig = new Rig();
        rig.Type("vie");
        rig.Controller.OnMouseDown();
        rig.Keyboard.Press(new Rune('e'));
        Assert.Equal("viee", rig.Screen.Text);
    }

    // Phím tắt

    [Fact]
    public void ModifierOnlyHotkeyTogglesOnRelease()
    {
        using var rig = new Rig();
        rig.Controller.OnModifiersChanged(ModifierKeys.Ctrl);
        rig.Controller.OnModifiersChanged(ModifierKeys.Ctrl | ModifierKeys.Shift);
        Assert.True(rig.Controller.Vietnamese);
        rig.Controller.OnModifiersChanged(ModifierKeys.Ctrl); // nhả Shift
        Assert.False(rig.Controller.Vietnamese);
        Assert.Equal("tieengs", rig.Type("tieengs"));
    }

    [Fact]
    public void ModifierOnlyHotkeyIsCancelledByAnyKeyPress()
    {
        using var rig = new Rig();
        rig.Controller.OnModifiersChanged(ModifierKeys.Ctrl | ModifierKeys.Shift);
        rig.Keyboard.Press('T', ModifierKeys.Ctrl | ModifierKeys.Shift, new Rune('T'));
        rig.Controller.OnModifiersChanged(ModifierKeys.Ctrl);
        Assert.True(rig.Controller.Vietnamese);
    }

    [Fact]
    public void KeyHotkeyIsSwallowedAndToggles()
    {
        using var rig = new Rig(s => s.Hotkey = Hotkey.CtrlSpace);
        var d = rig.Keyboard.Press(VirtualKeys.Space, ModifierKeys.Ctrl);
        Assert.Equal(KeyDecision.Swallow, d);
        Assert.False(rig.Controller.Vietnamese);
        Assert.Equal("", rig.Screen.Text);
        rig.Keyboard.Press(VirtualKeys.Space, ModifierKeys.Ctrl);
        Assert.True(rig.Controller.Vietnamese);
    }

    [Fact]
    public void ToggleIsPersistedAndPlaysSoundWhenEnabled()
    {
        using var rig = new Rig(s => s.SoundOnToggle = true);
        rig.Controller.Toggle();
        Assert.False(rig.Prefs.Current.IsVietnamese);
        Assert.Equal([false], rig.System.Sounds);
        rig.Controller.SetVietnamese(true, userInitiated: false);
        Assert.Equal([false], rig.System.Sounds);
    }

    // Theo ứng dụng

    [Fact]
    public void RemembersStatePerApplication()
    {
        using var rig = new Rig();
        rig.Foreground.SetForeground("code.exe");
        rig.Controller.Toggle();
        Assert.False(rig.Controller.Vietnamese);

        rig.Foreground.SetForeground("notepad.exe");
        Assert.True(rig.Controller.Vietnamese);

        rig.Foreground.SetForeground("code.exe");
        Assert.False(rig.Controller.Vietnamese);
    }

    [Fact]
    public void AppRulesForceModes()
    {
        using var rig = new Rig(s =>
        {
            s.AppRules["cmd.exe"] = new AppRule { Mode = AppMode.Off };
            s.AppRules["word.exe"] = new AppRule { Mode = AppMode.Vietnamese };
            s.AppRules["game.exe"] = new AppRule { Mode = AppMode.English };
        });
        rig.Foreground.SetForeground("cmd.exe");
        Assert.False(rig.Controller.IsActive);
        rig.Controller.Toggle(); // bị khóa khi chế độ Tắt
        Assert.False(rig.Controller.Vietnamese);
        Assert.Equal("as", rig.Type("as"));

        rig.Foreground.SetForeground("word.exe");
        Assert.True(rig.Controller.Vietnamese);

        rig.Foreground.SetForeground("game.exe");
        Assert.False(rig.Controller.Vietnamese);
    }

    [Fact]
    public void AppRuleCanOverrideCharset()
    {
        using var rig = new Rig(s => s.AppRules["vnword.exe"] = new AppRule { Charset = Charset.Tcvn3 });
        Assert.Equal("tiếng", rig.Type("tieengs"));
        rig.Screen.Clear();
        rig.Foreground.SetForeground("vnword.exe");
        Assert.Equal(Charset.Tcvn3.Encode("tiếng"), rig.Type("tieengs"));
    }

    [Fact]
    public void ChangingRulesWhileRunningTakesEffectImmediately()
    {
        using var rig = new Rig();
        rig.Foreground.SetForeground("a.exe");
        rig.Prefs.Update(s => s.AppRules["a.exe"] = new AppRule { Mode = AppMode.Off });
        Assert.False(rig.Controller.IsActive);
    }

    // Tạm dừng

    [Fact]
    public void PausesInPasswordFields()
    {
        using var rig = new Rig();
        int notifications = 0;
        rig.Controller.StateChanged += () => notifications++;
        rig.Foreground.IsSecureInput = true;
        Assert.Equal("as", rig.Type("as"));
        Assert.True(rig.Controller.SecureInput);
        Assert.True(notifications > 0);

        rig.Foreground.IsSecureInput = false;
        rig.Screen.Clear();
        Assert.Equal("á", rig.Type("as"));
    }

    [Fact]
    public void PausesInFullscreenOnlyWhenEnabled()
    {
        using var rig = new Rig();
        rig.Foreground.Fullscreen = true;
        rig.Foreground.SetForeground("game.exe");
        Assert.True(rig.Controller.IsActive);

        rig.Prefs.Update(s => s.DisableInFullscreen = true);
        rig.Controller.RefreshFullscreen();
        Assert.True(rig.Controller.FullscreenSuspended);
        Assert.Equal("as", rig.Type("as"));

        rig.Foreground.Fullscreen = false;
        rig.Controller.RefreshFullscreen();
        Assert.False(rig.Controller.FullscreenSuspended);
    }

    // Tương thích ứng dụng

    [Theory]
    [InlineData("chrome.exe", true, 0)]
    [InlineData("msedge.exe", true, 0)]
    [InlineData("Code.exe", false, 1500)]
    [InlineData("slack.exe", false, 1500)]
    [InlineData("notepad.exe", false, 0)]
    public void SendOptionsFollowTheApplication(string app, bool autocompleteFix, int delay)
    {
        using var rig = new Rig();
        rig.Foreground.SetForeground(app);
        rig.Type("as");
        Assert.Equal(autocompleteFix, rig.Screen.LastOptions!.ClearAutocompleteSelection);
        Assert.Equal(delay, rig.Screen.LastOptions.DelayMicros);
    }

    [Fact]
    public void AutocompleteFixCanBeDisabled()
    {
        using var rig = new Rig(s => s.BrowserAutocompleteFix = false);
        rig.Foreground.SetForeground("chrome.exe");
        rig.Type("as");
        Assert.False(rig.Screen.LastOptions!.ClearAutocompleteSelection);
    }

    // Bảng mã & gõ tắt

    [Fact]
    public void CompositeUnicodeWorksWithCodePointBackspace()
    {
        using var rig = new Rig(s => s.Charset = Charset.UnicodeComposite);
        Assert.Equal("tiếng việt ".Normalize(NormalizationForm.FormD), rig.Type("tieengs vieetj "));
    }

    [Fact]
    public void MacrosExpandAndReloadLive()
    {
        using var rig = new Rig();
        rig.Prefs.SetMacros(new MacroTable([KeyValuePair.Create("vn", "Việt Nam")]));
        Assert.Equal("Việt Nam ", rig.Type("vn "));
    }

    [Fact]
    public void ChangingInputMethodTakesEffectWithoutRestart()
    {
        using var rig = new Rig();
        Assert.Equal("a1", rig.Type("a1"));
        rig.Screen.Clear();
        rig.Prefs.Update(s => s.Method = InputMethod.Vni);
        Assert.Equal("á", rig.Type("a1"));
    }

    [Fact]
    public void UnprintableKeysResetTheWordButModifiersDoNot()
    {
        using var rig = new Rig();
        rig.Type("vie");
        rig.Keyboard.Press(VirtualKeys.Shift); // phím bổ trợ không có ký tự
        Assert.Equal("viê", rig.Type("e"));
        rig.Keyboard.Press(0x70); // F1 không có ký tự
        Assert.DoesNotContain("viế", rig.Type("s"));
    }

    [Fact]
    public async Task SurvivesConcurrentSettingsChangesWhileTyping()
    {
        using var rig = new Rig();
        var cts = new CancellationTokenSource();
        var toggler = Task.Run(() =>
        {
            int i = 0;
            while (!cts.IsCancellationRequested)
            {
                rig.Prefs.Update(s => s.Method = i++ % 2 == 0 ? InputMethod.Vni : InputMethod.Telex);
                rig.Foreground.SetForeground(i % 2 == 0 ? "a.exe" : "b.exe");
            }
        });
        var typing = Task.Run(() =>
        {
            for (int i = 0; i < 300; i++) rig.Controller.OnKeyDown(VirtualKeyboard.KeyFor(new Rune("tieengs"[i % 7])));
        });
        await typing.WaitAsync(TimeSpan.FromSeconds(30));
        await cts.CancelAsync();
        await toggler.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
