using System.Runtime.InteropServices;
using static KVietKey.Platform.Windows.NativeMethods;

namespace KVietKey.Platform.Windows;

/// <summary>Vị trí con trỏ chuột (pixel màn hình) — dùng để đặt panel gần biểu tượng khay.</summary>
public static partial class WindowsCursor
{
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT point);

    public static (int X, int Y)? TryGet() => GetCursorPos(out var p) ? (p.x, p.y) : null;
}
