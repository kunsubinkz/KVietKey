namespace KVietKey.Core;

/// <summary>Mã phím ảo của Windows (VK_*) dùng trong bộ gõ.</summary>
public static class VirtualKeys
{
    public const int Back = 0x08;
    public const int Tab = 0x09;
    public const int Return = 0x0D;
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Menu = 0x12; // Alt
    public const int Capital = 0x14;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int PageUp = 0x21;
    public const int PageDown = 0x22;
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int Insert = 0x2D;
    public const int Delete = 0x2E;
    public const int NumLock = 0x90;
    public const int ScrollLock = 0x91;
    public const int LWin = 0x5B;
    public const int RWin = 0x5C;
    public const int LShift = 0xA0;
    public const int RShift = 0xA1;
    public const int LControl = 0xA2;
    public const int RControl = 0xA3;
    public const int LMenu = 0xA4;
    public const int RMenu = 0xA5;

    /// <summary>Phím điều hướng/chỉnh sửa làm con trỏ rời khỏi từ đang gõ.</summary>
    public static readonly IReadOnlySet<int> Navigation = new HashSet<int>
    {
        Escape, Delete, Insert, Home, End, PageUp, PageDown, Left, Right, Down, Up,
    };

    /// <summary>Phím kết thúc từ nhưng vẫn được chuyển tiếp nguyên vẹn (Enter và Enter trên bàn phím số dùng chung mã).</summary>
    public static readonly IReadOnlySet<int> WordBreakers = new HashSet<int> { Return, Tab };

    /// <summary>Phím bổ trợ hoặc phím khóa (CapsLock…): bấm vào không làm con trỏ rời khỏi từ đang gõ.</summary>
    public static bool IsModifier(int vk) => vk is Shift or Control or Menu or LWin or RWin
        or LShift or RShift or LControl or RControl or LMenu or RMenu or Capital or NumLock or ScrollLock;

    public static string Name(int vk)
    {
        if (vk is >= 'A' and <= 'Z' or >= '0' and <= '9') return ((char)vk).ToString();
        return vk switch
        {
            Space => "Space",
            Return => "Enter",
            Tab => "Tab",
            Back => "Backspace",
            Escape => "Esc",
            >= 0x70 and <= 0x87 => "F" + (vk - 0x70 + 1),
            0xBA => ";",
            0xBB => "=",
            0xBC => ",",
            0xBD => "-",
            0xBE => ".",
            0xBF => "/",
            0xC0 => "`",
            0xDB => "[",
            0xDC => "\\",
            0xDD => "]",
            0xDE => "'",
            _ => $"#{vk}",
        };
    }
}
