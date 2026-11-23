namespace KVietKey.Core;

/// <summary>Hồ sơ tương thích theo tên file exe (viết thường).</summary>
public static class AppCompat
{
    public static readonly IReadOnlySet<string> Browsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "vivaldi.exe",
        "chromium.exe", "arc.exe", "iexplore.exe", "waterfox.exe", "librewolf.exe", "zen.exe",
    };

    public static readonly IReadOnlySet<string> Electron = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "slack.exe", "discord.exe", "code.exe", "code - insiders.exe", "notion.exe", "ms-teams.exe", "teams.exe",
        "figma.exe", "cursor.exe", "obsidian.exe", "whatsapp.exe", "signal.exe", "postman.exe", "spotify.exe",
    };

    /// <summary>Các bộ gõ khác dễ xung đột khi chạy cùng lúc (tên process không kèm .exe).</summary>
    public static readonly IReadOnlyList<string> ConflictingInputApps =
        ["UniKeyNT", "UniKey", "EVKey", "EVKey64", "OpenKey", "OpenKey64", "GoTiengViet", "GoNhanh", "Unikey"];
}
