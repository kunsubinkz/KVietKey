using System.Text;
using KVietKey.Engine;

namespace KVietKey.Core;

/// <summary>
/// Điều phối: nhận sự kiện bàn phím từ lớp nền tảng → gọi engine → gửi kết quả; quản lý trạng thái Việt/Anh.
/// Mọi phương thức công khai an toàn khi gọi từ nhiều luồng (luồng hook, luồng giao diện, luồng sự kiện foreground).
/// </summary>
public sealed class InputController
{
    private readonly object _gate = new();
    private readonly Preferences _prefs;
    private readonly IInputSender _sender;
    private readonly IForegroundMonitor _monitor;
    private readonly ISystemServices _system;
    private readonly Engine.Engine _engine = new();

    private bool _vietnamese;
    private bool _secureInput;
    private bool _fullscreenSuspended;
    private bool _hotkeyArmed;

    /// <summary>Chỉ dùng khi chẩn đoán (<c>--trace-file</c>): ghi vết từng sự kiện.</summary>
    public static Action<string>? Trace { get; set; }

    /// <summary>Tạm thời bỏ qua phím tắt chuyển Việt/Anh (khi người dùng đang ghi phím tắt mới).</summary>
    public volatile bool HotkeysSuspended;

    /// <summary>Thay đổi trạng thái (Việt/Anh, tạm dừng…). Có thể được gọi từ luồng bất kỳ.</summary>
    public event Action? StateChanged;

    public InputController(Preferences prefs, IInputSender sender, IForegroundMonitor monitor, ISystemServices system)
    {
        _prefs = prefs;
        _sender = sender;
        _monitor = monitor;
        _system = system;
        _vietnamese = prefs.Current.IsVietnamese;
        prefs.SettingsChanged += (o, n) => SettingsChanged(o, n);
        prefs.MacrosChanged += () => { lock (_gate) _engine.Macros = _prefs.Macros; };
        monitor.Changed += AppChanged;
        lock (_gate) ApplyConfigLocked();
        AppChanged();
    }

    // Trạng thái

    public Preferences Prefs => _prefs;

    public bool Vietnamese { get { lock (_gate) return _vietnamese; } }
    public bool SecureInput { get { lock (_gate) return _secureInput; } }
    public bool FullscreenSuspended { get { lock (_gate) return _fullscreenSuspended; } }
    public bool ForegroundElevated => _monitor.IsForegroundElevated;
    public string? ForegroundApp => _monitor.ProcessName;

    /// <summary>Đang thực sự biến đổi phím (bật tiếng Việt, không bị tạm dừng).</summary>
    public bool IsActive { get { lock (_gate) return IsActiveLocked(); } }

    private AppRule? CurrentRuleLocked() =>
        _monitor.ProcessName is { } name && _prefs.Current.AppRules.TryGetValue(name, out var rule) ? rule : null;

    private bool IsActiveLocked() =>
        _vietnamese && !_secureInput && !_fullscreenSuspended && CurrentRuleLocked()?.Mode != AppMode.Off;

    public void Stop()
    {
        lock (_gate) _engine.Reset();
    }

    public void SetVietnamese(bool on, bool userInitiated = true)
    {
        Settings after;
        bool playSound;
        lock (_gate)
        {
            if (userInitiated && CurrentRuleLocked()?.Mode == AppMode.Off) return;
            if (_vietnamese == on) return;
            _vietnamese = on;
            _engine.Reset();
            var app = _monitor.ProcessName;
            _prefs.Update(s =>
            {
                s.IsVietnamese = on;
                if (userInitiated && s.RememberAppState && app is not null) s.LastAppState[app] = on;
            });
            after = _prefs.Current;
            playSound = userInitiated && after.SoundOnToggle;
        }
        if (playSound) _system.PlayToggleSound(on);
        StateChanged?.Invoke();
    }

    public void Toggle() => SetVietnamese(!Vietnamese);

    private void ApplyConfigLocked()
    {
        var settings = _prefs.Current;
        _engine.Config = settings.ToEngineConfig(CurrentRuleLocked()?.Charset);
        _engine.Macros = _prefs.Macros;
        _engine.Reset();
    }

    private void SettingsChanged(Settings old, Settings updated)
    {
        lock (_gate)
        {
            ApplyConfigLocked();
            if (!RulesEqual(old.AppRules, updated.AppRules)) AppChangedLocked();
        }
        StateChanged?.Invoke();
    }

    private static bool RulesEqual(Dictionary<string, AppRule> a, Dictionary<string, AppRule> b) =>
        a.Count == b.Count && a.All(e => b.TryGetValue(e.Key, out var v) && v == e.Value);

    private void AppChanged()
    {
        lock (_gate) AppChangedLocked();
        StateChanged?.Invoke();
    }

    private void AppChangedLocked()
    {
        _engine.Reset();
        var s = _prefs.Current;
        if (_monitor.ProcessName is { } id)
        {
            switch (s.AppRules.TryGetValue(id, out var rule) ? rule.Mode : AppMode.Auto)
            {
                case AppMode.Vietnamese: _vietnamese = true; break;
                case AppMode.English or AppMode.Off: _vietnamese = false; break;
                default:
                    if (s.RememberAppState) _vietnamese = !s.LastAppState.TryGetValue(id, out var last) || last;
                    break;
            }
        }
        _fullscreenSuspended = s.DisableInFullscreen && _monitor.IsFullscreen();
        ApplyConfigLocked();
    }

    /// <summary>Kiểm tra lại trạng thái toàn màn hình (gọi định kỳ khi bật "tạm dừng khi toàn màn hình").</summary>
    public void RefreshFullscreen()
    {
        bool changed;
        lock (_gate)
        {
            var now = _prefs.Current.DisableInFullscreen && _monitor.IsFullscreen();
            changed = now != _fullscreenSuspended;
            _fullscreenSuspended = now;
            if (changed) _engine.Reset();
        }
        if (changed) StateChanged?.Invoke();
    }

    // Xử lý sự kiện

    /// <summary>Click chuột: con trỏ đã đổi chỗ nên bỏ từ đang gõ.</summary>
    public void OnMouseDown()
    {
        lock (_gate) _engine.Reset();
    }

    /// <summary>Phím bổ trợ đổi trạng thái: nhận biết phím tắt chỉ gồm phím bổ trợ (vd. Ctrl+Shift bấm rồi nhả).</summary>
    public void OnModifiersChanged(ModifierKeys current)
    {
        bool toggle = false;
        lock (_gate)
        {
            var hotkey = _prefs.Current.Hotkey;
            if (hotkey.KeyCode is not null || HotkeysSuspended)
            {
                _hotkeyArmed = false;
                return;
            }
            if (current == hotkey.Mods)
            {
                _hotkeyArmed = true;
            }
            else
            {
                toggle = _hotkeyArmed;
                _hotkeyArmed = false;
            }
        }
        if (toggle) Toggle();
    }

    public KeyDecision OnKeyDown(in KeyInput key)
    {
        bool toggle = false;
        KeyDecision decision;
        bool notify = false;
        lock (_gate)
        {
            _hotkeyArmed = false;
            var hotkey = _prefs.Current.Hotkey;
            if (!HotkeysSuspended && hotkey.KeyCode is { } hk && key.VirtualKey == hk && key.Modifiers == hotkey.Mods)
            {
                toggle = true;
                decision = KeyDecision.Swallow;
            }
            else
            {
                bool secure = _monitor.IsSecureInput;
                if (secure != _secureInput)
                {
                    _secureInput = secure;
                    _engine.Reset();
                    notify = true;
                }
                decision = IsActiveLocked() ? HandleActiveKeyLocked(key) : Passed(key, "bỏ qua (không hoạt động)");
            }
        }
        if (toggle) Toggle();
        if (notify) StateChanged?.Invoke();
        return decision;
    }

    private static KeyDecision Passed(in KeyInput key, string why)
    {
        Trace?.Invoke($"down vk={key.VirtualKey} {why}");
        return KeyDecision.Pass;
    }

    private KeyDecision HandleActiveKeyLocked(in KeyInput key)
    {
        if (key.Modifiers.HasFlag(ModifierKeys.Ctrl) || key.Modifiers.HasFlag(ModifierKeys.Alt)
            || key.Modifiers.HasFlag(ModifierKeys.Win))
        {
            _engine.Reset();
            return KeyDecision.Pass;
        }
        if (key.VirtualKey == VirtualKeys.Back)
        {
            _engine.Backspace();
            return KeyDecision.Pass;
        }
        if (VirtualKeys.Navigation.Contains(key.VirtualKey))
        {
            _engine.Reset();
            return KeyDecision.Pass;
        }
        if (VirtualKeys.WordBreakers.Contains(key.VirtualKey))
        {
            return Emit(_engine.ProcessBreak(key.VirtualKey == VirtualKeys.Tab ? '\t' : '\n'), key);
        }
        if (key.Character is not { } ch)
        {
            if (!VirtualKeys.IsModifier(key.VirtualKey)) _engine.Reset();
            Trace?.Invoke($"down vk={key.VirtualKey} không có ký tự → reset");
            return KeyDecision.Pass;
        }
        var result = _engine.Process(ch);
        Trace?.Invoke($"down vk={key.VirtualKey} ch={ch} → xoá={result.Backspaces} gửi={result.Text} choQua={result.Passthrough} từ={_engine.CurrentWord} app={_monitor.ProcessName ?? "?"}");
        return Emit(result, key);
    }

    private KeyDecision Emit(EngineResult result, in KeyInput key)
    {
        if (result.IsEmpty) return result.Passthrough ? KeyDecision.Pass : KeyDecision.Swallow;
        _sender.Send(result.Backspaces, result.Text, SendOptionsFor(_monitor.ProcessName));
        if (_prefs.Current.DebugLog)
        {
            // Chỉ ghi số liệu, không bao giờ ghi nội dung gõ.
            System.Diagnostics.Trace.WriteLine($"[KVietKey] emit backspaces={result.Backspaces} chars={result.Text.Length} passthrough={result.Passthrough}");
        }
        if (result.Passthrough) _sender.Repost(key);
        return KeyDecision.Swallow;
    }

    private SendOptions SendOptionsFor(string? app)
    {
        if (app is null) return SendOptions.Default;
        var s = _prefs.Current;
        return new SendOptions
        {
            ClearAutocompleteSelection = AppCompat.Browsers.Contains(app) && s.BrowserAutocompleteFix,
            DelayMicros = AppCompat.Electron.Contains(app) ? s.ElectronDelayMicros : 0,
        };
    }
}
