using Avalonia.Controls;
using Avalonia.Input.Platform;
using KVietKey.Core;

namespace KVietKey.App.UI;

internal static class ClipboardConverter
{
    /// <summary>Đọc văn bản trong clipboard, đổi bảng mã rồi ghi lại. Trả về false nếu clipboard không có văn bản.</summary>
    public static async Task<bool> ConvertAsync(TopLevel top, CharsetConversion conversion)
    {
        var clipboard = top.Clipboard;
        if (clipboard is null) return false;
        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(text)) return false;
        await clipboard.SetTextAsync(conversion.Apply(text));
        return true;
    }
}
