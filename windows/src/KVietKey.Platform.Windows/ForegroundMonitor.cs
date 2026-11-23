using System.Diagnostics;
using System.Runtime.InteropServices;
using KVietKey.Core;
using static KVietKey.Platform.Windows.NativeMethods;

namespace KVietKey.Platform.Windows;

internal static class ProcessInfo
{
    /// <summary>Tên file exe viết thường của process (vd. <c>chrome.exe</c>); null nếu không mở được.</summary>
    public static unsafe string? ImageName(uint pid)
    {
        nint process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == 0) return null;
        try
        {
            char* buffer = stackalloc char[1024];
            uint size = 1024;
            if (!QueryFullProcessImageName(process, 0, buffer, ref size)) return null;
            return Path.GetFileName(new string(buffer, 0, (int)size)).ToLowerInvariant();
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>Process đang chạy với quyền admin. Không mở được token (bị từ chối) cũng coi là quyền cao hơn.</summary>
    public static bool IsElevated(uint pid)
    {
        nint process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == 0) return true;
        try
        {
            if (!OpenProcessToken(process, TOKEN_QUERY, out nint token)) return true;
            try
            {
                return GetTokenInformation(token, TokenElevation, out var elevation, Marshal.SizeOf<TOKEN_ELEVATION>(), out _)
                    && elevation.TokenIsElevated != 0;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }

    public static bool CurrentProcessIsElevated() => IsElevated((uint)Environment.ProcessId);
}

/// <summary>Theo dõi cửa sổ foreground và ô đang focus bằng WinEvent hook (không đọc trong callback của hook bàn phím).</summary>
public sealed class ForegroundMonitor : IForegroundMonitor, IDisposable
{
    private readonly UiAutomation _uia;
    private readonly bool _selfElevated = ProcessInfo.CurrentProcessIsElevated();
    private readonly WinEventProc _callback;
    private readonly MessageLoopThread _loop;
    private nint _foregroundHook;
    private nint _focusHook;

    private volatile string? _processName;
    private volatile bool _elevated;
    private volatile bool _secure;
    private int _secureVersion;

    public event Action? Changed;

    public ForegroundMonitor(UiAutomation uia)
    {
        _uia = uia;
        _callback = OnWinEvent;
        _loop = new MessageLoopThread("KVietKey.Foreground", Install, Uninstall);
    }

    public string? ProcessName => _processName;
    public bool IsSecureInput => _secure;
    public bool IsForegroundElevated => _elevated && !_selfElevated;

    public void Start()
    {
        UpdateFromForegroundWindow(raise: false);
        _loop.Start();
    }

    public void Dispose() => _loop.Stop();

    private void Install()
    {
        nint callback = Marshal.GetFunctionPointerForDelegate(_callback);
        const uint flags = WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS;
        _foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, 0, callback, 0, 0, flags);
        _focusHook = SetWinEventHook(EVENT_OBJECT_FOCUS, EVENT_OBJECT_FOCUS, 0, callback, 0, 0, flags);
        if (_foregroundHook == 0) throw new InvalidOperationException("SetWinEventHook thất bại");
    }

    private void Uninstall()
    {
        if (_foregroundHook != 0) UnhookWinEvent(_foregroundHook);
        if (_focusHook != 0) UnhookWinEvent(_focusHook);
        _foregroundHook = _focusHook = 0;
    }

    private void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            if (eventType == EVENT_SYSTEM_FOREGROUND) UpdateFromForegroundWindow(raise: true);
            RequestSecureCheck();
        }
        catch (Exception e)
        {
            Debug.WriteLine($"[ForegroundMonitor] {e}");
        }
    }

    private void UpdateFromForegroundWindow(bool raise)
    {
        nint hwnd = GetForegroundWindow();
        if (hwnd == 0) return;
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0 || pid == (uint)Environment.ProcessId) return;
        var name = ProcessInfo.ImageName(pid);
        _elevated = ProcessInfo.IsElevated(pid);
        if (name == _processName && !raise) return;
        _processName = name;
        if (raise) Changed?.Invoke();
    }

    /// <summary>
    /// Mỗi lần focus đổi, coi ô mới là nhạy cảm cho tới khi UI Automation trả lời (vài chục ms) để không
    /// biến đổi nhầm những phím đầu tiên của mật khẩu.
    /// </summary>
    private void RequestSecureCheck()
    {
        int version = Interlocked.Increment(ref _secureVersion);
        _secure = true;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            bool password = _uia.IsFocusedElementPassword();
            if (version == Volatile.Read(ref _secureVersion)) _secure = password;
        });
    }

    public bool IsFullscreen()
    {
        if (SHQueryUserNotificationState(out int state) == 0 && state is 3 or 4) return true; // D3D toàn màn hình / đang trình chiếu

        nint hwnd = GetForegroundWindow();
        if (hwnd == 0 || hwnd == GetShellWindow() || hwnd == GetDesktopWindow()) return false;
        if (!GetWindowRect(hwnd, out var rect)) return false;
        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info)) return false;
        var m = info.rcMonitor;
        bool coversMonitor = rect.left <= m.left && rect.top <= m.top && rect.right >= m.right && rect.bottom >= m.bottom;
        // Cửa sổ phóng to bình thường vẫn có thanh tiêu đề (WS_CAPTION) và rect của nó tràn ra viền vô hình khi thanh
        // tác vụ tự ẩn; toàn màn hình thật (game, F11, trình chiếu) không có.
        bool hasCaption = (GetWindowLongPtr(hwnd, GWL_STYLE) & WS_CAPTION) == WS_CAPTION;
        return coversMonitor && !hasCaption && !IsShellClass(hwnd);
    }

    private static unsafe bool IsShellClass(nint hwnd)
    {
        char* buffer = stackalloc char[64];
        int n = GetClassName(hwnd, buffer, 64);
        var name = new string(buffer, 0, n);
        return name is "Progman" or "WorkerW" or "Shell_TrayWnd";
    }
}
