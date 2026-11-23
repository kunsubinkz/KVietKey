using System.Runtime.Versioning;
using KVietKey.Core;

[assembly: SupportedOSPlatform("windows")]

namespace KVietKey.Platform.Windows;

/// <summary>
/// Theo dõi trạng thái phím bổ trợ từ chính các sự kiện của hook (GetAsyncKeyState chưa cập nhật bên trong
/// callback của hook mức thấp). Chỉ dùng từ luồng hook.
/// </summary>
internal sealed class ModifierTracker
{
    private bool _lShift, _rShift, _lCtrl, _rCtrl, _lAlt, _rAlt, _lWin, _rWin;

    public ModifierKeys Current =>
        (_lShift || _rShift ? ModifierKeys.Shift : 0)
        | (_lCtrl || _rCtrl ? ModifierKeys.Ctrl : 0)
        | (_lAlt || _rAlt ? ModifierKeys.Alt : 0)
        | (_lWin || _rWin ? ModifierKeys.Win : 0);

    public bool IsShift => _lShift || _rShift;

    /// <summary>Cập nhật theo một sự kiện phím; trả về true nếu <paramref name="vk"/> là phím bổ trợ.</summary>
    public bool Update(int vk, bool down)
    {
        switch (vk)
        {
            case VirtualKeys.LShift: _lShift = down; return true;
            case VirtualKeys.RShift: _rShift = down; return true;
            case VirtualKeys.LControl: _lCtrl = down; return true;
            case VirtualKeys.RControl: _rCtrl = down; return true;
            case VirtualKeys.LMenu: _lAlt = down; return true;
            case VirtualKeys.RMenu: _rAlt = down; return true;
            case VirtualKeys.LWin: _lWin = down; return true;
            case VirtualKeys.RWin: _rWin = down; return true;
            case VirtualKeys.Shift or VirtualKeys.Control or VirtualKeys.Menu: return true;
            default: return false;
        }
    }

    /// <summary>
    /// Đồng bộ lại với trạng thái thật của hệ thống (gọi khi hook mới bắt đầu hoặc sau khoảng thời gian không nhận
    /// được sự kiện), tránh kẹt phím bổ trợ nếu từng bỏ lỡ sự kiện nhả phím.
    /// </summary>
    public void SyncFromSystem()
    {
        static bool Down(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;
        _lShift = Down(VirtualKeys.LShift);
        _rShift = Down(VirtualKeys.RShift);
        _lCtrl = Down(VirtualKeys.LControl);
        _rCtrl = Down(VirtualKeys.RControl);
        _lAlt = Down(VirtualKeys.LMenu);
        _rAlt = Down(VirtualKeys.RMenu);
        _lWin = Down(VirtualKeys.LWin);
        _rWin = Down(VirtualKeys.RWin);
    }
}
