using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace KVietKey.App.UI;

/// <summary>Bảng màu theo thiết kế "KVietKey — Giao diện".</summary>
internal static class KV
{
    private static IBrush Brush(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));

    public static readonly IBrush Blue = Brush("#1F5BFF");
    public static readonly IBrush Navy = Brush("#0B2A6F");
    public static readonly IBrush Ink = Brush("#0B1B3A");
    public static readonly IBrush Muted = Brush("#5B6B8C");
    public static readonly IBrush Text2 = Brush("#3C4D72");
    public static readonly IBrush Bg = Brush("#F4F8FF");
    public static readonly IBrush Border = Brush("#E3EAF7");
    public static readonly IBrush Border2 = Brush("#D9E4F7");
    public static readonly IBrush KeyBorder = Brush("#C7D3EA");
    public static readonly IBrush Soft = Brush("#EEF3FF");
    public static readonly IBrush Soft2 = Brush("#E3EDFF");
    public static readonly IBrush Banner = Brush("#E8F0FF");
    public static readonly IBrush Cyan = Brush("#7DE3F5");
    public static readonly IBrush CyanDot = Brush("#00B8D9");
    public static readonly IBrush CyanSoft = Brush("#CFF3FA");
    public static readonly IBrush NavyText = Brush("#C4D4FF");
    public static readonly IBrush White = Brush("#FFFFFF");
    public static readonly IBrush White08 = Brush("#14FFFFFF");
    public static readonly IBrush White18 = Brush("#2EFFFFFF");
    public static readonly IBrush Warn = Brush("#E07B00");
    public static readonly IBrush Green = Brush("#22A559");

    public static readonly (IBrush Bg, IBrush Fg)[] Tiles =
    [
        (Brush("#E3EDFF"), Brush("#1F5BFF")),
        (Brush("#CFF3FA"), Brush("#0A5C6E")),
        (Brush("#DDE4FF"), Brush("#2B3FA8")),
    ];

    public const string FontUri = "avares://KVietKey/Assets/Fonts#Be Vietnam Pro";
    public static readonly FontFamily Font = new(FontUri);
}
