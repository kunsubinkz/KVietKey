using System.Text;
using KVietKey.Core;

namespace KVietKey.Platform.Fake;

/// <summary>
/// Ô văn bản giả lập cho phép chạy bộ gõ không cần Windows (test, xem trước UI trên macOS).
/// Mô phỏng đúng cách ứng dụng Windows xử lý: Backspace xóa một code point, phím được cho qua thì thêm ký tự.
/// </summary>
public sealed class FakeInputSender : IInputSender
{
    private readonly StringBuilder _text = new();

    /// <summary>Mỗi lần gọi Send/Repost: lịch sử thao tác để test thứ tự sự kiện.</summary>
    public List<string> Log { get; } = [];

    public string Text => _text.ToString();

    public SendOptions? LastOptions { get; private set; }

    public event Action? TextChanged;

    public void Clear()
    {
        _text.Clear();
        TextChanged?.Invoke();
    }

    public void Send(int backspaces, string text, SendOptions options)
    {
        LastOptions = options;
        Log.Add($"send bs={backspaces} text={text}");
        for (int i = 0; i < backspaces; i++) Backspace();
        _text.Append(text);
        TextChanged?.Invoke();
    }

    public void Repost(KeyInput key)
    {
        Log.Add($"repost vk={key.VirtualKey}");
        ApplyPassedKey(key);
    }

    /// <summary>Hiệu ứng của một phím đã được cho đi tiếp tới ứng dụng.</summary>
    public void ApplyPassedKey(in KeyInput key)
    {
        if (key.VirtualKey == VirtualKeys.Back) Backspace();
        else if (key.VirtualKey == VirtualKeys.Return) _text.Append('\n');
        else if (key.VirtualKey == VirtualKeys.Tab) _text.Append('\t');
        else if (key.Character is { } ch && key.Modifiers is ModifierKeys.None or ModifierKeys.Shift) _text.Append(ch.ToString());
        TextChanged?.Invoke();
    }

    private void Backspace()
    {
        if (_text.Length == 0) return;
        int remove = char.IsLowSurrogate(_text[^1]) && _text.Length >= 2 ? 2 : 1;
        _text.Length -= remove;
    }
}

public sealed class FakeForegroundMonitor : IForegroundMonitor
{
    public string? ProcessName { get; private set; } = "notepad.exe";
    public bool IsSecureInput { get; set; }
    public bool IsForegroundElevated { get; set; }
    public bool Fullscreen { get; set; }

    public bool IsFullscreen() => Fullscreen;

    public event Action? Changed;

    public void SetForeground(string? processName)
    {
        ProcessName = processName?.ToLowerInvariant();
        Changed?.Invoke();
    }
}

public sealed class FakeSystemServices : ISystemServices
{
    public bool IsLaunchAtLoginEnabled { get; private set; }
    public List<bool> Sounds { get; } = [];
    public List<string> Conflicts { get; } = [];

    public void SetLaunchAtLogin(bool enabled) => IsLaunchAtLoginEnabled = enabled;
    public void PlayToggleSound(bool vietnameseOn) => Sounds.Add(vietnameseOn);
    public IReadOnlyList<string> FindConflictingInputApps() => Conflicts;
}

/// <summary>Bàn phím ảo: biến chuỗi ký tự thành các sự kiện phím đưa vào <see cref="InputController"/>, như hook thật.</summary>
public sealed class VirtualKeyboard(InputController controller, FakeInputSender target)
{
    /// <summary>Gõ chuỗi. <c>\b</c> = Backspace, <c>\n</c> = Enter, <c>\t</c> = Tab; chữ hoa tính là có Shift.</summary>
    public void Type(string text)
    {
        foreach (var rune in text.EnumerateRunes()) Press(rune);
    }

    public void Press(Rune rune)
    {
        var key = KeyFor(rune);
        if (controller.OnKeyDown(key) == KeyDecision.Pass) target.ApplyPassedKey(key);
    }

    public KeyDecision Press(int virtualKey, ModifierKeys modifiers = ModifierKeys.None, Rune? character = null)
    {
        var key = new KeyInput(virtualKey, modifiers, character);
        var decision = controller.OnKeyDown(key);
        if (decision == KeyDecision.Pass) target.ApplyPassedKey(key);
        return decision;
    }

    public static KeyInput KeyFor(Rune rune)
    {
        char c = rune.IsBmp ? (char)rune.Value : '\0';
        return c switch
        {
            '\b' => new KeyInput(VirtualKeys.Back, ModifierKeys.None, null),
            '\n' => new KeyInput(VirtualKeys.Return, ModifierKeys.None, null),
            '\t' => new KeyInput(VirtualKeys.Tab, ModifierKeys.None, null),
            ' ' => new KeyInput(VirtualKeys.Space, ModifierKeys.None, rune),
            >= 'a' and <= 'z' => new KeyInput(char.ToUpperInvariant(c), ModifierKeys.None, rune),
            >= 'A' and <= 'Z' => new KeyInput(c, ModifierKeys.Shift, rune),
            >= '0' and <= '9' => new KeyInput(c, ModifierKeys.None, rune),
            _ => new KeyInput(0xDE, ModifierKeys.None, rune),
        };
    }
}
