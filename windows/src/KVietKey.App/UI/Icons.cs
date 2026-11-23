using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace KVietKey.App.UI;

/// <summary>Biểu tượng nét (kiểu Feather, hệ toạ độ 24×24) dùng chung cho sidebar và menu panel.</summary>
internal static class Icons
{
    public const string Sliders = "M3 6h8 M15 6h6 M3 12h4 M11 12h10 M3 18h12 M19 18h2 M11 6a2 2 0 1 0 4 0a2 2 0 1 0 -4 0 M7 12a2 2 0 1 0 4 0a2 2 0 1 0 -4 0 M15 18a2 2 0 1 0 4 0a2 2 0 1 0 -4 0";
    public const string Type = "M4 7V5h16v2 M12 5v14 M9 19h6";
    public const string Grid = "M4 4h6v6H4z M14 4h6v6h-6z M4 14h6v6H4z M14 14h6v6h-6z";
    public const string Bolt = "M13 2L4 14h7l-1 8 9-12h-7z";
    public const string Shield = "M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z";
    public const string Info = "M12 3a9 9 0 1 0 0.001 0z M12 11v5 M12 7.5v0.01";
    public const string Check = "M5 12.5l4.5 4.5L19 7.5";
    public const string Keyboard = "M3 6h18v12H3z M7 10h0.01 M11 10h0.01 M15 10h0.01 M7 14h10";
    public const string AppWindow = "M3 5h18v14H3z M3 9h18";
    public const string Trash = "M4 7h16 M10 11v6 M14 11v6 M6 7l1 13h10l1-13 M9 7V4h6v3";
    public const string Pencil = "M4 20h4L19 9l-4-4L4 16z M13.5 6.5l4 4";
    public const string Lock = "M6 11h12v9H6z M8 11V8a4 4 0 0 1 8 0v3";

    /// <summary>Tạo biểu tượng; nếu có <paramref name="follow"/> thì màu nét theo <c>Foreground</c> của điều khiển đó.</summary>
    public static Control Create(string data, double size = 18, IBrush? stroke = null, Control? follow = null, double thickness = 1.8)
    {
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(data),
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            StrokeThickness = thickness,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Stroke = stroke ?? KV.Ink,
        };
        if (follow is not null)
            path.Bind(Avalonia.Controls.Shapes.Shape.StrokeProperty, follow.GetObservable(Avalonia.Controls.Primitives.TemplatedControl.ForegroundProperty));
        return path;
    }
}
