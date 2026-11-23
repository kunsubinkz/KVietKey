using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using KVietKey.Core;

namespace KVietKey.App.UI;

/// <summary>
/// Ô ghi phím tắt chuyển Việt ⇄ Anh. Hỗ trợ tổ hợp chỉ gồm phím bổ trợ (≥ 2 phím, vd. Ctrl + Shift: bấm rồi nhả)
/// hoặc tổ hợp phím bổ trợ + một phím.
/// </summary>
internal sealed class HotkeyRecorder : UserControl
{
    private readonly AppServices _s;
    private readonly Binder _binder;
    private readonly Border _display = new() { Height = 36 };
    private readonly Button _button;
    private readonly HashSet<Key> _down = [];
    private bool _recording;
    private ModifierKeys _max;
    private TopLevel? _top;

    public HotkeyRecorder(AppServices services, Binder binder)
    {
        _s = services;
        _binder = binder;
        _button = new Button();
        _button.Classes.Add("k");
        _button.Classes.Add("soft");
        _button.Click += (_, _) => { if (_recording) Stop(); else Start(); };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Height = 44 };
        _display.VerticalAlignment = VerticalAlignment.Center;
        _display.HorizontalAlignment = HorizontalAlignment.Left;
        grid.Children.Add(_display);
        Grid.SetColumn(_button, 1);
        grid.Children.Add(_button);
        Content = grid;

        binder.Add(Render);
        DetachedFromVisualTree += (_, _) => Stop();
    }

    private void Render()
    {
        _button.Content = _recording ? Loc.Tr("Hủy", "Cancel") : Loc.Tr("Ghi phím mới", "Record new shortcut");
        _display.Child = _recording
            ? Ui.Label(Loc.Tr("Nhấn tổ hợp phím… (Esc để hủy)", "Press shortcut… (Esc to cancel)"), 13, FontWeight.Normal, KV.Text2)
            : Ui.KeyCaps(_s.Prefs.Current.Hotkey);
        if (_recording) _display.Child.VerticalAlignment = VerticalAlignment.Center;
    }

    private void Start()
    {
        _top = TopLevel.GetTopLevel(this);
        if (_top is null) return;
        _recording = true;
        _max = ModifierKeys.None;
        _down.Clear();
        _s.Controller.HotkeysSuspended = true;
        _top.AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _top.AddHandler(KeyUpEvent, OnKeyUp, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Render();
    }

    private void Stop()
    {
        if (!_recording) return;
        _recording = false;
        _s.Controller.HotkeysSuspended = false;
        if (_top is not null)
        {
            _top.RemoveHandler(KeyDownEvent, OnKeyDown);
            _top.RemoveHandler(KeyUpEvent, OnKeyUp);
        }
        Render();
    }

    private void Commit(Hotkey hotkey)
    {
        _s.Prefs.Update(s => s.Hotkey = hotkey);
        Stop();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (e.Key == Key.Escape) { Stop(); return; }

        if (KeyMap.ModifierOf(e.Key) is { } mod)
        {
            _down.Add(e.Key);
            var now = KeyMap.FromAvalonia(e.KeyModifiers) | mod;
            if (KeyMap.PopCount(now) >= KeyMap.PopCount(_max)) _max = now;
            return;
        }

        var mods = KeyMap.FromAvalonia(e.KeyModifiers);
        if (mods != ModifierKeys.None && KeyMap.ToVirtualKey(e.Key) is { } vk)
            Commit(new Hotkey { Modifiers = (int)mods, KeyCode = vk });
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (KeyMap.ModifierOf(e.Key) is null) return;
        _down.Remove(e.Key);
        if (_down.Count == 0 && KeyMap.PopCount(_max) >= 2)
            Commit(new Hotkey { Modifiers = (int)_max });
    }
}
