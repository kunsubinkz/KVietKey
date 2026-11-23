using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using KVietKey.Core;
using KVietKey.Engine;

namespace KVietKey.App.UI;

/// <summary>Biểu tượng khay hệ thống (chip V/E) và menu chuột phải.</summary>
internal sealed class TrayController : IDisposable
{
    private readonly AppServices _s;
    private readonly Action _showPanel;
    private readonly TrayIcon _tray = new();
    private readonly NativeMenuItem _toggle = new();
    private readonly NativeMenuItem _settings = new();
    private readonly NativeMenuItem _macros = new();
    private readonly NativeMenuItem _quit = new();
    private readonly Dictionary<(bool Vi, bool Paused), WindowIcon> _icons = [];

    public TrayController(AppServices services, Action showPanel)
    {
        _s = services;
        _showPanel = showPanel;

        _toggle.Click += (_, _) => _s.Controller.Toggle();
        _settings.Click += (_, _) => _s.OpenSettings(SettingsTab.Basic);
        _macros.Click += (_, _) => _s.OpenSettings(SettingsTab.Macros);
        _quit.Click += (_, _) => _s.Quit();
        _tray.Menu = new NativeMenu { Items = { _toggle, new NativeMenuItemSeparator(), _macros, _settings, new NativeMenuItemSeparator(), _quit } };
        _tray.Clicked += (_, _) => _showPanel();

        if (Application.Current is { } app) TrayIcon.SetIcons(app, new TrayIcons { _tray });
        _s.Controller.StateChanged += OnStateChanged;
        _s.Prefs.SettingsChanged += (_, _) => Ui.OnUi(Refresh);
        Refresh();
    }

    private void OnStateChanged() => Ui.OnUi(Refresh);

    public void Refresh()
    {
        var c = _s.Controller;
        bool paused = c.SecureInput || c.FullscreenSuspended;
        _tray.IsVisible = _s.Prefs.Current.ShowTrayIcon;
        _tray.Icon = IconFor(c.Vietnamese, paused);

        var method = _s.Prefs.Current.Method.DisplayName();
        var tip = c.Vietnamese ? Loc.Tr("Đang gõ tiếng Việt", "Vietnamese on") + $" ({method})" : Loc.Tr("Đang gõ tiếng Anh", "English");
        if (c.SecureInput) tip += " — " + Loc.Tr("tạm dừng (ô mật khẩu)", "paused (password field)");
        if (c.FullscreenSuspended) tip += " — " + Loc.Tr("tạm dừng (toàn màn hình)", "paused (full screen)");
        if (c.ForegroundElevated) tip += " — " + Loc.Tr("ứng dụng chạy quyền admin: không gõ được", "app runs as admin: typing unavailable");
        _tray.ToolTipText = tip;

        _toggle.Header = c.Vietnamese ? Loc.Tr("Chuyển sang tiếng Anh", "Switch to English") : Loc.Tr("Chuyển sang tiếng Việt", "Switch to Vietnamese");
        _settings.Header = Loc.Tr("Cài đặt…", "Settings…");
        _macros.Header = Loc.Tr("Gõ tắt…", "Macros…");
        _quit.Header = Loc.Tr("Thoát KVietKey", "Quit KVietKey");
    }

    private WindowIcon IconFor(bool vi, bool paused)
    {
        if (_icons.TryGetValue((vi, paused), out var icon)) return icon;
        return _icons[(vi, paused)] = new WindowIcon(RenderChip(vi ? "V" : "E", vi, paused));
    }

    /// <summary>V: chip xanh chữ trắng (màu thương hiệu). E: chip xám navy. Tạm dừng: chip nhạt hơn.</summary>
    public static Bitmap RenderChip(string letter, bool active, bool paused, int size = 64)
    {
        IBrush fill = paused ? new ImmutableSolidColorBrush(Color.Parse("#AEBBD6"))
            : active ? new ImmutableSolidColorBrush(Color.Parse("#1F5BFF"))
            : new ImmutableSolidColorBrush(Color.Parse("#3C4D72"));
        var chip = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.22), Background = fill,
            Child = new TextBlock
            {
                Text = letter, FontSize = size * 0.62, FontWeight = FontWeight.Bold, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
        };
        chip.Measure(new Size(size, size));
        chip.Arrange(new Rect(0, 0, size, size));
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        bitmap.Render(chip);
        return bitmap;
    }

    public void Dispose()
    {
        _s.Controller.StateChanged -= OnStateChanged;
        _tray.IsVisible = false;
        _tray.Dispose();
    }
}
