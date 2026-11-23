using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using KVietKey.Core;
using KVietKey.Engine;
using InputMethod = KVietKey.Engine.InputMethod;

namespace KVietKey.App.UI;

/// <summary>Panel bo góc hiện gần biểu tượng khay khi bấm trái vào icon V/E.</summary>
internal sealed class MenuPanelWindow : Window
{
    private const double PanelWidth = 360;

    private readonly AppServices _s;
    private readonly Binder _binder = new();
    private readonly TextBlock _status = Ui.Label("", 12, FontWeight.Normal, KV.Muted);
    private readonly List<(bool Value, Button Button, Border Badge, TextBlock Letter, TextBlock Title)> _lang = [];
    private readonly Action _onStateChanged;
    private bool _closing;
    private long _hiddenAtTicks;

    public MenuPanelWindow(AppServices services)
    {
        _s = services;
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        ShowActivated = true;
        Icon = AppIcon.Load();

        Content = Build();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Hide(); };
        Deactivated += (_, _) =>
        {
            if (_closing) return;
            _hiddenAtTicks = Environment.TickCount64;
            Hide();
        };

        _onStateChanged = () => Ui.OnUi(Refresh);
        _s.Controller.StateChanged += _onStateChanged;
        _s.Prefs.SettingsChanged += (_, _) => Ui.OnUi(Refresh);
        Closed += (_, _) => _s.Controller.StateChanged -= _onStateChanged;
    }

    public void ShowNear()
    {
        Refresh();
        if (!IsVisible) Show();
        Position = ComputePosition();
        Activate();
    }

    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }
        // Bấm vào icon khay khi panel đang mở: panel vừa tự ẩn do mất focus, đừng bật lại ngay.
        if (Environment.TickCount64 - _hiddenAtTicks < 300) return;
        ShowNear();
    }

    private PixelPoint ComputePosition()
    {
        var cursor = _s.CursorPosition();
        var anchor = cursor is { } c ? new PixelPoint(c.X, c.Y) : new PixelPoint(0, 0);
        var screens = Screens;
        var screen = (cursor is null ? screens.Primary : screens.ScreenFromPoint(anchor)) ?? screens.Primary;
        if (screen is null) return new PixelPoint(0, 0);

        var wa = screen.WorkingArea;
        double scale = screen.Scaling;
        int w = (int)((Bounds.Width > 0 ? Bounds.Width : PanelWidth + 20) * scale);
        int h = (int)((Bounds.Height > 0 ? Bounds.Height : 480) * scale);
        const int margin = 8;

        int x = cursor is null ? wa.Right - w - margin : Math.Clamp(anchor.X - w / 2, wa.X + margin, wa.Right - w - margin);
        int y;
        if (cursor is null || anchor.Y >= wa.Bottom - h / 2) y = wa.Bottom - h - margin;      // khay ở cạnh dưới
        else if (anchor.Y <= wa.Y + h / 2) y = wa.Y + margin;                                 // khay ở cạnh trên
        else y = Math.Clamp(anchor.Y - h / 2, wa.Y + margin, wa.Bottom - h - margin);        // khay ở cạnh bên
        return new PixelPoint(x, y);
    }

    private Control Build()
    {
        var settings = Ui.Primary("", () => { });
        settings.Classes.Remove("primary");
        settings.Classes.Add("soft");
        settings.Width = 36;
        settings.Height = 36;
        settings.Padding = new Thickness(0);
        settings.Content = Icons.Create(Icons.Sliders, 16, KV.Blue);
        ToolTip.SetTip(settings, Loc.Tr("Mở cài đặt", "Open settings"));
        settings.Click += (_, _) => { Hide(); _s.OpenSettings(SettingsTab.Basic); };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        var title = Ui.VStack(2, Ui.Label("KVietKey", 16, FontWeight.Bold), _status);
        title.VerticalAlignment = VerticalAlignment.Center;
        var logo = Ui.Logo(40);
        Grid.SetColumn(title, 1);
        Grid.SetColumn(settings, 2);
        header.Children.Add(logo);
        header.Children.Add(title);
        header.Children.Add(settings);

        // Công tắc Việt / Anh
        var langGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 4 };
        langGrid.Children.Add(LangButton("V", "Tiếng Việt", true, 0));
        langGrid.Children.Add(LangButton("E", "English", false, 1));
        var langBox = new Border { Background = KV.Soft, CornerRadius = new CornerRadius(18), Padding = new Thickness(4), Child = langGrid };

        var shortcut = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(4, 0) };
        var shortcutLabel = Ui.Label(Loc.Tr("Chuyển nhanh bằng phím tắt", "Quick switch shortcut"), 12, FontWeight.Normal, KV.Muted);
        shortcutLabel.VerticalAlignment = VerticalAlignment.Center;
        var keys = new ContentControl();
        _binder.Add(() => keys.Content = Ui.KeyCaps(_s.Prefs.Current.Hotkey, large: false));
        Grid.SetColumn(keys, 1);
        shortcut.Children.Add(shortcutLabel);
        shortcut.Children.Add(keys);

        var method = Ui.VStack(6,
            new TextBlock { Text = Loc.Tr("Kiểu gõ", "Input method"), FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = KV.Muted, Margin = new Thickness(2, 0, 0, 0) },
            Ui.Segmented(_binder, Enum.GetValues<InputMethod>().Select(m => (m, Pages.MethodTitle(m))).ToList(),
                () => _s.Prefs.Current.Method, v => _s.Prefs.Update(x => x.Method = v), height: 34, fontSize: 13));

        var toggles = Ui.VStack(0,
            Ui.ToggleRow(_binder, Loc.Tr("Kiểm tra chính tả", "Spell check"), () => _s.Prefs.Current.SpellCheck,
                v => _s.Prefs.Update(x => x.SpellCheck = v), null, false, 34),
            Ui.ToggleRow(_binder, Loc.Tr("Tự khôi phục từ tiếng Anh", "Auto-restore English words"), () => _s.Prefs.Current.AutoRestore,
                v => _s.Prefs.Update(x => x.AutoRestore = v), null, false, 34),
            Ui.ToggleRow(_binder, Loc.Tr("Đặt dấu kiểu mới (hoà, thuý)", "Modern tone placement (hoà, thuý)"), () => _s.Prefs.Current.ModernTone,
                v => _s.Prefs.Update(x => x.ModernTone = v), null, false, 34));

        var actions = Ui.VStack(2,
            ActionRow(Loc.Tr("Gõ tắt…", "Macros…"), "Ctrl+M", () => { Hide(); _s.OpenSettings(SettingsTab.Macros); }),
            ActionRow(Loc.Tr("Cài đặt…", "Settings…"), "Ctrl+,", () => { Hide(); _s.OpenSettings(SettingsTab.Basic); }),
            ActionRow(Loc.Tr("Thoát KVietKey", "Quit KVietKey"), "Ctrl+Q", () => { _closing = true; _s.Quit(); }));

        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.M, KeyModifiers.Control), Command = new Cmd(() => { Hide(); _s.OpenSettings(SettingsTab.Macros); }) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Control), Command = new Cmd(() => { Hide(); _s.OpenSettings(SettingsTab.Basic); }) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Q, KeyModifiers.Control), Command = new Cmd(() => { _closing = true; _s.Quit(); }) });

        var stack = Ui.VStack(12, header, langBox, shortcut, method, CharsetRow(), toggles, Ui.Divider(KV.Border), actions);
        return new Border
        {
            Margin = new Thickness(10),
            Width = PanelWidth,
            Padding = new Thickness(16),
            Background = KV.White,
            CornerRadius = new CornerRadius(20),
            BoxShadow = BoxShadows.Parse("0 8 24 0 #33000000"),
            Child = stack,
        };
    }

    private Control LangButton(string letter, string title, bool value, int column)
    {
        var letterText = Ui.Label(letter, 14, FontWeight.Bold, KV.Blue);
        letterText.HorizontalAlignment = HorizontalAlignment.Center;
        letterText.VerticalAlignment = VerticalAlignment.Center;
        var badge = Ui.Rect(KV.White, 9, 28, 28, letterText);
        var titleText = Ui.Label(title, 15, FontWeight.SemiBold, KV.Text2);
        titleText.VerticalAlignment = VerticalAlignment.Center;
        var row = Ui.HStack(10, badge, titleText);
        row.HorizontalAlignment = HorizontalAlignment.Center;

        var button = new Button { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch };
        button.Classes.Add("k");
        button.Classes.Add("lang");
        button.Click += (_, _) => _s.Controller.SetVietnamese(value);
        Grid.SetColumn(button, column);
        _lang.Add((value, button, badge, letterText, titleText));
        return button;
    }

    private Control ActionRow(string title, string shortcut, Action onClick)
    {
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var t = Ui.Label(title, 14, FontWeight.Medium);
        var k = Ui.Label(shortcut, 12, FontWeight.Normal, KV.Muted);
        t.VerticalAlignment = k.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(k, 1);
        g.Children.Add(t);
        g.Children.Add(k);
        var b = new Button { Content = g, HorizontalAlignment = HorizontalAlignment.Stretch };
        b.Classes.Add("k");
        b.Classes.Add("row");
        b.Click += (_, _) => onClick();
        return b;
    }

    private Control CharsetRow()
    {
        var label = Ui.Label("", 14, FontWeight.SemiBold, KV.Blue);
        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 6 };
        var title = Ui.Label(Loc.Tr("Bảng mã", "Charset"), 14, FontWeight.Medium);
        var chevron = Ui.Label("⌄", 13, FontWeight.Bold, KV.Muted);
        title.VerticalAlignment = label.VerticalAlignment = chevron.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(label, 1);
        Grid.SetColumn(chevron, 2);
        g.Children.Add(title);
        g.Children.Add(label);
        g.Children.Add(chevron);
        _binder.Add(() => label.Text = _s.Prefs.Current.Charset.DisplayName());

        var button = new Button { Content = g, HorizontalAlignment = HorizontalAlignment.Stretch, Background = KV.Bg };
        button.Classes.Add("k");
        button.Classes.Add("row");

        var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        foreach (var cs in Enum.GetValues<Charset>())
        {
            var item = new MenuItem { Header = cs.DisplayName() };
            item.Click += (_, _) => _s.Prefs.Update(x => x.Charset = cs);
            flyout.Items.Add(item);
        }
        flyout.Items.Add(new Separator());
        var convert = new MenuItem { Header = Loc.Tr("Chuyển mã văn bản đã copy", "Convert copied text") };
        foreach (var conv in CharsetConversion.All)
        {
            var item = new MenuItem { Header = conv.Title };
            item.Click += async (_, _) =>
            {
                _closing = true; // menu con lấy focus: đừng tự ẩn panel giữa chừng
                try { await ClipboardConverter.ConvertAsync(this, conv); }
                finally { _closing = false; Hide(); }
            };
            convert.Items.Add(item);
        }
        flyout.Items.Add(convert);
        button.Flyout = flyout;
        return button;
    }

    private void Refresh()
    {
        var vi = _s.Controller.Vietnamese;
        var method = _s.Prefs.Current.Method.DisplayName();
        _status.Text = _s.Controller.SecureInput ? Loc.Tr("Tạm dừng · ô nhập mật khẩu", "Paused · password field")
            : _s.Controller.FullscreenSuspended ? Loc.Tr("Tạm dừng · toàn màn hình", "Paused · full screen")
            : vi ? Loc.Tr("Đang bật · ", "On · ") + method
            : Loc.Tr("Đang tắt tiếng Việt", "Vietnamese is off");

        foreach (var (value, button, badge, letter, title) in _lang)
        {
            bool selected = vi == value;
            button.Classes.Set("on", selected);
            badge.Background = selected ? new ImmutableSolidColorBrush(Color.Parse("#38FFFFFF")) : KV.White;
            letter.Foreground = selected ? KV.White : KV.Blue;
            title.Foreground = selected ? KV.White : KV.Text2;
        }
        _binder.Refresh();
    }

    private sealed class Cmd(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => action();
    }
}
