using System.Diagnostics;
using System.Runtime.InteropServices;
using KVietKey.Core;
using static KVietKey.Platform.Windows.NativeMethods;

namespace KVietKey.Platform.Windows;

/// <summary>
/// Hook bàn phím và chuột mức thấp (WH_KEYBOARD_LL / WH_MOUSE_LL) chạy trên luồng riêng.
/// Callback phải trả về thật nhanh: không gọi UI Automation hay I/O trong đó, nếu không Windows sẽ gỡ hook.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const nuint WatchdogTimerId = 1;
    private const uint WatchdogIntervalMs = 30_000;
    private const int VK_PACKET = 0xE7;
    private const int VK_PROCESSKEY = 0xE5;

    private readonly InputController _controller;
    private readonly MessageLoopThread _loop;
    private readonly HookProc _keyboardProc;
    private readonly HookProc _mouseProc;
    private readonly ModifierTracker _modifiers = new();
    private readonly HashSet<int> _swallowed = [];

    private nint _keyboardHook;
    private nint _mouseHook;
    private ModifierKeys _lastModifiers;
    private long _lastEventTicks = Environment.TickCount64;

    public KeyboardHook(InputController controller)
    {
        _controller = controller;
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
        _loop = new MessageLoopThread("KVietKey.KeyboardHook", Install, Uninstall, OnLoopMessage);
    }

    /// <summary>Cài hook. Trả về false nếu Windows từ chối.</summary>
    public bool Start()
    {
        try
        {
            _loop.Start();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public void Stop() => _loop.Stop();

    public void Dispose() => Stop();

    private void Install()
    {
        InstallKeyboardHook();
        if (_keyboardHook == 0) throw new InvalidOperationException($"SetWindowsHookEx thất bại ({Marshal.GetLastWin32Error()})");
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, Marshal.GetFunctionPointerForDelegate(_mouseProc), GetModuleHandle(null), 0);
        _modifiers.SyncFromSystem();
        _lastModifiers = _modifiers.Current;
        MessageLoopThread.SetThreadTimer(WatchdogTimerId, WatchdogIntervalMs);
    }

    private void InstallKeyboardHook() =>
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, Marshal.GetFunctionPointerForDelegate(_keyboardProc), GetModuleHandle(null), 0);

    private void Uninstall()
    {
        if (_keyboardHook != 0) UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != 0) UnhookWindowsHookEx(_mouseHook);
        _keyboardHook = _mouseHook = 0;
    }

    /// <summary>
    /// Windows có thể âm thầm gỡ hook nếu một callback từng chạy quá lâu. Định kỳ cài lại khi đang rảnh
    /// (gỡ cái cũ trước rồi mới cài cái mới để một phím không bị xử lý hai lần).
    /// </summary>
    private void OnLoopMessage(MSG msg)
    {
        if (msg.message != WM_TIMER || msg.wParam != WatchdogTimerId) return;
        if (Environment.TickCount64 - _lastEventTicks < 2000) return;
        if (_keyboardHook != 0) UnhookWindowsHookEx(_keyboardHook);
        InstallKeyboardHook();
        _modifiers.SyncFromSystem();
        _swallowed.Clear();
    }

    private unsafe nint KeyboardProc(int nCode, nuint wParam, nint lParam)
    {
        if (nCode == HC_ACTION)
        {
            try
            {
                if (HandleKeyboard((uint)wParam, in *(KBDLLHOOKSTRUCT*)lParam)) return 1;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[KeyboardHook] {e}");
            }
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private nint MouseProc(int nCode, nuint wParam, nint lParam)
    {
        if (nCode == HC_ACTION)
        {
            uint message = (uint)wParam;
            if (message is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN)
            {
                try { _controller.OnMouseDown(); }
                catch (Exception e) { Debug.WriteLine($"[MouseHook] {e}"); }
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    /// <summary>Trả về true nếu phải nuốt sự kiện.</summary>
    private bool HandleKeyboard(uint message, in KBDLLHOOKSTRUCT kb)
    {
        // Sự kiện do chính KVietKey gửi: cho đi qua nguyên vẹn.
        if ((kb.flags & LLKHF_INJECTED) != 0 && kb.dwExtraInfo == InputSender.Magic) return false;

        _lastEventTicks = Environment.TickCount64;
        int vk = (int)kb.vkCode;
        bool isDown = message is WM_KEYDOWN or WM_SYSKEYDOWN;
        bool isUp = message is WM_KEYUP or WM_SYSKEYUP;
        if (!isDown && !isUp) return false;

        if (vk is VK_PACKET or VK_PROCESSKEY) return false;

        if (_modifiers.Update(vk, isDown))
        {
            var now = _modifiers.Current;
            if (now != _lastModifiers)
            {
                _lastModifiers = now;
                _controller.OnModifiersChanged(now);
            }
            return false;
        }

        if (isUp) return _swallowed.Remove(vk);

        if (VirtualKeys.IsModifier(vk)) return false; // CapsLock, NumLock…

        var key = BuildKeyInput(vk, kb);
        if (_controller.OnKeyDown(in key) == KeyDecision.Swallow)
        {
            _swallowed.Add(vk);
            return true;
        }
        _swallowed.Remove(vk);
        return false;
    }

    private KeyInput BuildKeyInput(int vk, in KBDLLHOOKSTRUCT kb)
    {
        var mods = _modifiers.Current;
        bool extended = (kb.flags & LLKHF_EXTENDED) != 0;
        System.Text.Rune? character = null;
        if (!mods.HasFlag(ModifierKeys.Ctrl) && !mods.HasFlag(ModifierKeys.Alt) && !mods.HasFlag(ModifierKeys.Win)
            && !VirtualKeys.Navigation.Contains(vk) && vk != VirtualKeys.Back)
        {
            bool caps = (GetKeyState(0x14) & 1) != 0;
            character = KeyTranslator.Translate(kb.vkCode, kb.scanCode, mods.HasFlag(ModifierKeys.Shift),
                false, false, caps, ForegroundKeyboardLayout());
        }
        return new KeyInput(vk, mods, character, (int)kb.scanCode, extended);
    }

    private static nint ForegroundKeyboardLayout()
    {
        uint thread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        return GetKeyboardLayout(thread);
    }
}
