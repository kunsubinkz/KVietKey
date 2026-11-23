using Avalonia.Input;
using KVietKey.App;
using KVietKey.App.UI;
using KVietKey.Core;

namespace KVietKey.App.Tests;

public class CliTests
{
    [Fact]
    public void ParsesFlagsAndValues()
    {
        var cli = Cli.Parse(["--autostart", "--data-dir", "C:\\x", "--trace-file", "t.log", "--allow-multiple"]);
        Assert.True(cli.Autostart);
        Assert.True(cli.AllowMultiple);
        Assert.Equal("C:\\x", cli.DataDirectory);
        Assert.Equal("t.log", cli.TraceFile);
        Assert.False(cli.SelfTest);
        Assert.Null(cli.RenderUiDirectory);
    }

    [Fact]
    public void ValueFlagWithoutValueIsIgnored()
    {
        Assert.Null(Cli.Parse(["--render-ui"]).RenderUiDirectory);
        Assert.True(Cli.Parse(["--selftest"]).SelfTest);
    }
}

public class SingleInstanceTests
{
    [Fact]
    public async Task SecondInstanceSendsCommandToFirst()
    {
        var name = "kvk-" + Guid.NewGuid().ToString("N")[..8]; // ngắn: đường dẫn socket trên macOS bị giới hạn 104 ký tự
        using var first = new SingleInstance(name);
        Assert.True(first.IsPrimary);

        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        first.Listen(cmd => received.TrySetResult(cmd));

        using var second = new SingleInstance(name);
        Assert.False(second.IsPrimary);

        // Pipe server có thể chưa kịp mở: thử lại một chút.
        bool sent = false;
        for (int i = 0; i < 20 && !sent; i++)
        {
            sent = second.SendToPrimary("show", 500);
            if (!sent) await Task.Delay(100);
        }
        Assert.True(sent);
        Assert.Equal("show", await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}

public class KeyMapTests
{
    [Theory]
    [InlineData(Key.A, 'A')]
    [InlineData(Key.Z, 'Z')]
    [InlineData(Key.D0, '0')]
    [InlineData(Key.D9, '9')]
    [InlineData(Key.F1, 0x70)]
    [InlineData(Key.F12, 0x7B)]
    [InlineData(Key.Space, VirtualKeys.Space)]
    [InlineData(Key.OemComma, 0xBC)]
    public void MapsToWindowsVirtualKeys(Key key, int expected)
    {
        Assert.Equal(expected, KeyMap.ToVirtualKey(key));
    }

    [Fact]
    public void UnsupportedKeysAreRejected()
    {
        Assert.Null(KeyMap.ToVirtualKey(Key.LeftCtrl));
        Assert.Null(KeyMap.ToVirtualKey(Key.Escape));
    }

    [Fact]
    public void ModifierKeysAndFlagsConvert()
    {
        Assert.Equal(ModifierKeys.Ctrl, KeyMap.ModifierOf(Key.RightCtrl));
        Assert.Equal(ModifierKeys.Win, KeyMap.ModifierOf(Key.LWin));
        Assert.Null(KeyMap.ModifierOf(Key.A));
        Assert.Equal(ModifierKeys.Ctrl | ModifierKeys.Shift, KeyMap.FromAvalonia(KeyModifiers.Control | KeyModifiers.Shift));
        Assert.Equal(2, KeyMap.PopCount(ModifierKeys.Ctrl | ModifierKeys.Alt));
    }
}

public class AppInfoTests
{
    [Theory]
    [InlineData("chrome.exe", "Chrome")]
    [InlineData("code - insiders.exe", "Code - insiders")]
    [InlineData("WINWORD.EXE", "WINWORD")]
    [InlineData("tool", "Tool")]
    public void DisplayNameIsReadable(string exe, string expected)
    {
        Assert.Equal(expected, AppInfo.DisplayName(exe));
    }
}
