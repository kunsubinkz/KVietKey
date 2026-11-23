using System.Text;

namespace KVietKey.Platform.Windows;

/// <summary>Chuyển mã phím + trạng thái bổ trợ thành ký tự thực sự được gõ, theo bố cục bàn phím của ứng dụng đang dùng.</summary>
internal static class KeyTranslator
{
    private const uint DoNotChangeKeyboardState = 0x4; // Windows 10 1607+

    /// <summary>Trả về null nếu phím không tạo ký tự in được (điều khiển, dead key, phím chức năng…).</summary>
    public static unsafe Rune? Translate(uint vk, uint scanCode, bool shift, bool ctrl, bool alt, bool capsLock, nint layout)
    {
        byte* state = stackalloc byte[256];
        for (int i = 0; i < 256; i++) state[i] = 0;
        if (shift) state[0x10] = 0x80;
        if (ctrl) state[0x11] = 0x80;
        if (alt) state[0x12] = 0x80;
        if (capsLock) state[0x14] = 0x01;

        char* buffer = stackalloc char[8];
        int n = NativeMethods.ToUnicodeEx(vk, scanCode, state, buffer, 8, DoNotChangeKeyboardState, layout);
        if (n <= 0) return null; // 0: không có ký tự, < 0: dead key

        var text = new ReadOnlySpan<char>(buffer, n);
        var runes = text.EnumerateRunes();
        if (!runes.MoveNext()) return null;
        var rune = runes.Current;
        if (runes.MoveNext()) return null; // nhiều ký tự (ligature/dead key ghép): để hệ thống tự xử lý
        if (rune.Value < 0x20 || rune.Value == 0x7F) return null;
        return rune;
    }

    public static uint ScanCodeFor(uint vk) => NativeMethods.MapVirtualKey(vk, 0 /* MAPVK_VK_TO_VSC */);
}
