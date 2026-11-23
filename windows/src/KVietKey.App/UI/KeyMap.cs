using Avalonia.Input;
using KVietKey.Core;

namespace KVietKey.App.UI;

/// <summary>Đổi mã phím của Avalonia sang mã phím ảo Windows (VK_*).</summary>
internal static class KeyMap
{
    public static int? ToVirtualKey(Key key)
    {
        if (key is >= Key.A and <= Key.Z) return 'A' + (key - Key.A);
        if (key is >= Key.D0 and <= Key.D9) return '0' + (key - Key.D0);
        if (key is >= Key.F1 and <= Key.F12) return 0x70 + (key - Key.F1);
        return key switch
        {
            Key.Space => VirtualKeys.Space,
            Key.Enter => VirtualKeys.Return,
            Key.Tab => VirtualKeys.Tab,
            Key.OemSemicolon => 0xBA,
            Key.OemPlus => 0xBB,
            Key.OemComma => 0xBC,
            Key.OemMinus => 0xBD,
            Key.OemPeriod => 0xBE,
            Key.OemQuestion => 0xBF,
            Key.OemTilde => 0xC0,
            Key.OemOpenBrackets => 0xDB,
            Key.OemPipe => 0xDC,
            Key.OemCloseBrackets => 0xDD,
            Key.OemQuotes => 0xDE,
            _ => null,
        };
    }

    public static ModifierKeys? ModifierOf(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Ctrl,
        Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
        Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
        Key.LWin or Key.RWin => ModifierKeys.Win,
        _ => null,
    };

    public static ModifierKeys FromAvalonia(KeyModifiers m) =>
        (m.HasFlag(KeyModifiers.Control) ? ModifierKeys.Ctrl : 0)
        | (m.HasFlag(KeyModifiers.Shift) ? ModifierKeys.Shift : 0)
        | (m.HasFlag(KeyModifiers.Alt) ? ModifierKeys.Alt : 0)
        | (m.HasFlag(KeyModifiers.Meta) ? ModifierKeys.Win : 0);

    public static int PopCount(ModifierKeys m) => System.Numerics.BitOperations.PopCount((uint)m);
}
