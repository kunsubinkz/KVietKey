using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using KVietKey.Core;
using KVietKey.Engine;

namespace KVietKey.App.UI;

/// <summary>Cửa sổ chào mừng 3 bước: Chào mừng → Tránh xung đột phím của Windows → Kiểu gõ.</summary>
internal sealed class OnboardingWindow : Window
{
    private readonly AppServices _s;
    private readonly Binder _binder = new();
    private readonly ContentControl _right = new();
    private int _step;

    /// <summary>Hook bàn phím đã cài được chưa (hiển thị ở bước 2).</summary>
    public bool InputRunning { get; set; } = true;

    public OnboardingWindow(AppServices services, int step = 1)
    {
        _s = services;
        _step = step;
        Title = "KVietKey";
        Width = 880;
        Height = 580;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = AppIcon.Load();
        Background = KV.White;

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("340,*") };
        grid.Children.Add(LeftPanel());
        _right.Margin = new Thickness(48, 40);
        Grid.SetColumn(_right, 1);
        grid.Children.Add(_right);
        Content = grid;

        _s.Prefs.SettingsChanged += OnSettingsChanged;
        Closed += (_, _) => _s.Prefs.SettingsChanged -= OnSettingsChanged;
        ShowStep(step);
    }

    private void OnSettingsChanged(Settings a, Settings b) => Ui.OnUi(_binder.Refresh);

    public void ShowStep(int step)
    {
        _step = step;
        Control body = step switch { 1 => Welcome(), 2 => Conflicts(), _ => MethodChoice() };
        _right.Content = new DockPanel { LastChildFill = true, Children = { }, }.WithChildren(Stepper(), body);
    }

    private Control LeftPanel()
    {
        Control Bullet(string text) => Ui.HStack(10,
            Icons.Create(Icons.Check, 14, KV.Cyan, thickness: 2.4),
            Ui.Label(text, 14, FontWeight.Normal, Brush("#E6EDFF")));

        var demo = new Border
        {
            Padding = new Thickness(16), CornerRadius = new CornerRadius(16), Background = KV.White08,
            BorderBrush = KV.White18, BorderThickness = new Thickness(1),
            Child = Ui.VStack(0,
                Ui.Label(Loc.Tr("Bạn gõ", "You type"), 13, FontWeight.Normal, KV.NavyText),
                new TextBlock { Text = "tieengs vieetj", FontSize = 16, FontWeight = FontWeight.Medium, Foreground = KV.White, Margin = new Thickness(0, 2, 0, 0) },
                new Border { Height = 1, Background = KV.White18, Margin = new Thickness(0, 12) },
                Ui.Label(Loc.Tr("Hiện ra", "You get"), 13, FontWeight.Normal, KV.NavyText),
                new TextBlock { Text = "tiếng việt", FontSize = 26, FontWeight = FontWeight.Bold, Foreground = KV.Cyan, Margin = new Thickness(0, 2, 0, 0) }),
        };

        var bullets = Ui.VStack(12,
            Bullet(Loc.Tr("Chạy gốc trên Windows x64 và ARM64", "Native on Windows x64 and ARM64")),
            Bullet(Loc.Tr("Không cần quyền quản trị", "No admin rights needed")),
            Bullet(Loc.Tr("Không lưu, không gửi nội dung gõ", "Never stores or sends what you type")));

        var top = Ui.VStack(24,
            Ui.HStack(12, Ui.Logo(44), CenterV(Ui.Label("KVietKey", 20, FontWeight.Bold, KV.White))),
            Ui.Label(Loc.Tr("Gõ tiếng Việt, nhẹ như không.", "Type Vietnamese, light as air."), 30, FontWeight.Bold, KV.White, wrap: true),
            demo);
        var dock = new DockPanel();
        DockPanel.SetDock(bullets, Dock.Bottom);
        dock.Children.Add(bullets);
        dock.Children.Add(top);
        return new Border { Background = KV.Navy, Padding = new Thickness(36, 40), Child = dock };
    }

    private static IBrush Brush(string hex) => new ImmutableSolidColorBrush(Color.Parse(hex));

    private static Control CenterV(Control c)
    {
        c.VerticalAlignment = VerticalAlignment.Center;
        return c;
    }

    // Thanh bước

    private Control Stepper()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*,Auto"), ColumnSpacing = 12, Margin = new Thickness(0, 0, 0, 24) };
        grid.Children.Add(Node(1, Loc.Tr("Chào mừng", "Welcome")));
        var l1 = Line(_step > 1);
        Grid.SetColumn(l1, 1);
        grid.Children.Add(l1);
        var n2 = Node(2, Loc.Tr("Phím tắt", "Shortcut"));
        Grid.SetColumn(n2, 2);
        grid.Children.Add(n2);
        var l2 = Line(_step > 2);
        Grid.SetColumn(l2, 3);
        grid.Children.Add(l2);
        var n3 = Node(3, Loc.Tr("Kiểu gõ", "Typing style"));
        Grid.SetColumn(n3, 4);
        grid.Children.Add(n3);
        DockPanel.SetDock(grid, Dock.Top);
        return grid;
    }

    private static Control Line(bool filled) => new Border
    {
        Height = 2, CornerRadius = new CornerRadius(1), VerticalAlignment = VerticalAlignment.Center,
        Background = filled ? KV.Blue : KV.Border2,
    };

    private Control Node(int n, string title)
    {
        bool done = _step > n, current = _step == n;
        Control inner = done
            ? Icons.Create(Icons.Check, 12, KV.White, thickness: 2.6)
            : new TextBlock { Text = n.ToString(), FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = current ? KV.Blue : KV.Muted };
        inner.HorizontalAlignment = HorizontalAlignment.Center;
        inner.VerticalAlignment = VerticalAlignment.Center;
        var circle = new Border
        {
            Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Child = inner,
            Background = done ? KV.Blue : Brushes.Transparent,
            BorderBrush = done || current ? KV.Blue : KV.KeyBorder, BorderThickness = new Thickness(2),
        };
        var label = Ui.Label(title, 13, FontWeight.SemiBold, done ? KV.Blue : current ? KV.Ink : KV.Muted);
        label.VerticalAlignment = VerticalAlignment.Center;
        return Ui.HStack(8, circle, label);
    }

    // Bước 1

    private Control Welcome()
    {
        Control Info(string icon, string text) => Ui.HStack(12,
            Ui.Rect(KV.Soft2, 10, 34, 34, new Border { Child = Icons.Create(icon, 16, KV.Blue), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }),
            CenterV(Ui.Label(text, 14, FontWeight.Medium)));

        var top = Ui.VStack(24,
            Ui.VStack(10,
                Ui.Label(Loc.Tr("Chào mừng đến với KVietKey", "Welcome to KVietKey"), 26, FontWeight.Bold),
                Ui.Label(Loc.Tr("Bộ gõ tiếng Việt viết mới hoàn toàn, nhẹ và chạy gốc trên Windows. Chỉ mất vài bước để bắt đầu.",
                    "A brand-new Vietnamese input method, light and native on Windows. It only takes a few steps to get started."),
                    15, FontWeight.Normal, KV.Text2, wrap: true, lineHeight: 24)),
            Ui.VStack(10,
                Info(Icons.Keyboard, Loc.Tr("Telex, VNI, VIQR và nhiều bảng mã", "Telex, VNI, VIQR and many charsets")),
                Info(Icons.AppWindow, Loc.Tr("Cài đặt riêng cho từng ứng dụng", "Per-app settings")),
                Info(Icons.Lock, Loc.Tr("Không có mã mạng, không thu thập dữ liệu", "No network code, no data collection"))));
        return Layout(top, Ui.Primary(Loc.Tr("Bắt đầu", "Get started"), () => ShowStep(2), big: true), null, Loc.Tr("Bước 1/3", "Step 1/3"));
    }

    // Bước 2

    private Control Conflicts()
    {
        var hotkey = _s.Prefs.Current.Hotkey.DisplayString;
        var path = new Border
        {
            Padding = new Thickness(16), CornerRadius = new CornerRadius(16), Background = KV.Bg,
            BorderBrush = KV.Border2, BorderThickness = new Thickness(1),
            Child = Ui.VStack(8,
                Ui.Label(Loc.Tr("Cài đặt › Thời gian & ngôn ngữ › Nhập › Cài đặt bàn phím nâng cao › Tùy chọn thanh ngôn ngữ › Cài đặt phím nâng cao › Giữa các ngôn ngữ nhập › Thay đổi trình tự phím › Chưa gán",
                    "Settings › Time & language › Typing › Advanced keyboard settings › Language bar options › Advanced Key Settings › Between input languages › Change Key Sequence › Not Assigned"),
                    12, FontWeight.Medium, KV.Muted, wrap: true, lineHeight: 18)),
        };

        var running = InputRunning;
        var status = Ui.HStack(10,
            new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = running ? KV.Green : KV.Warn, VerticalAlignment = VerticalAlignment.Center },
            CenterV(Ui.Label(running
                ? Loc.Tr("KVietKey đang bắt phím bình thường.", "KVietKey is capturing keystrokes.")
                : Loc.Tr("Không cài được bộ bắt phím. Hãy kiểm tra phần mềm diệt virus rồi mở lại KVietKey.", "Could not install the keyboard hook. Check your antivirus and relaunch KVietKey."),
                13, FontWeight.Normal, KV.Text2, wrap: true)));

        var top = Ui.VStack(24,
            Ui.VStack(10,
                Ui.Label(Loc.Tr("Tránh trùng phím tắt với Windows", "Avoid a shortcut clash with Windows"), 26, FontWeight.Bold, wrap: true),
                Ui.Label(Loc.Tr($"Windows mặc định dùng Ctrl + Shift (hoặc Alt + Shift) để đổi bàn phím. Phím tắt chuyển Việt ⇄ Anh của KVietKey hiện là {hotkey}, nên bạn nên tắt phím đổi bàn phím của Windows để hai bên không tranh nhau. Nếu máy chỉ có một bàn phím, có thể bỏ qua bước này.",
                    $"Windows uses Ctrl + Shift (or Alt + Shift) to switch keyboards by default. KVietKey's Vietnamese ⇄ English shortcut is currently {hotkey}, so you should turn off Windows' keyboard-switch shortcut to avoid a clash. If you only have one keyboard you can skip this step."),
                    15, FontWeight.Normal, KV.Text2, wrap: true, lineHeight: 24)),
            path, status);

        var open = Ui.Primary(Loc.Tr("Mở cài đặt bàn phím", "Open keyboard settings"), OpenKeyboardSettings, big: true);
        var later = Ui.TextButton(Loc.Tr("Tiếp tục", "Continue"), () => ShowStep(3));
        return Layout(top, open, later, Loc.Tr("Bước 2/3", "Step 2/3"));
    }

    private static void OpenKeyboardSettings()
    {
        try { Process.Start(new ProcessStartInfo("ms-settings:typing") { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            Debug.WriteLine(e.Message);
        }
    }

    // Bước 3

    private Control MethodChoice()
    {
        var list = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 10, RowSpacing = 10 };
        var rows = new List<(InputMethod Method, Button Button, TextBlock Title, Border Check)>();
        int cell = 0;
        foreach (var m in Enum.GetValues<InputMethod>())
        {
            var title = Ui.Label(Pages.MethodTitle(m), 16, FontWeight.Bold);
            var check = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Background = KV.Blue, VerticalAlignment = VerticalAlignment.Center,
                Child = Icons.Create(Icons.Check, 10, KV.White, thickness: 2.6),
            };
            ((Control)check.Child).HorizontalAlignment = HorizontalAlignment.Center;
            ((Control)check.Child).VerticalAlignment = VerticalAlignment.Center;
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(16, 12) };
            g.Children.Add(Ui.VStack(2, title, Ui.Label(Pages.MethodExample(m), 13, FontWeight.Normal, KV.Muted)));
            Grid.SetColumn(check, 1);
            g.Children.Add(check);
            var button = new Button
            {
                Content = g, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            };
            button.Classes.Add("k");
            var method = m;
            button.Click += (_, _) => _s.Prefs.Update(x => x.Method = method);
            Grid.SetColumn(button, cell % 2);
            Grid.SetRow(button, cell / 2);
            cell++;
            list.Children.Add(button);
            rows.Add((m, button, title, check));
        }
        _binder.Add(() =>
        {
            foreach (var (m, button, title, check) in rows)
            {
                bool on = _s.Prefs.Current.Method == m;
                button.Background = on ? KV.Soft : KV.White;
                button.BorderBrush = on ? KV.Blue : KV.Border;
                title.Foreground = on ? KV.Blue : KV.Ink;
                check.IsVisible = on;
            }
        });

        var hotkeyRow = Ui.HStack(8,
            CenterV(Ui.Label(Loc.Tr("Phím tắt chuyển Việt ⇄ Anh", "Toggle Vietnamese ⇄ English"), 13, FontWeight.Normal, KV.Muted)),
            CenterV(new ContentControl { Content = Ui.KeyCaps(_s.Prefs.Current.Hotkey, large: false) }));

        var top = Ui.VStack(20,
            Ui.VStack(10,
                Ui.Label(Loc.Tr("Chọn kiểu gõ quen thuộc", "Choose your typing style"), 26, FontWeight.Bold),
                Ui.Label(Loc.Tr("Bạn có thể đổi lại bất cứ lúc nào trong Cài đặt hoặc ngay trên biểu tượng ở khay hệ thống.",
                    "You can change it any time in Settings or right from the system tray icon."), 15, FontWeight.Normal, KV.Text2, wrap: true)),
            list, hotkeyRow);
        return Layout(top, Ui.Primary(Loc.Tr("Hoàn tất", "Finish"), Finish, big: true),
            Ui.TextButton(Loc.Tr("Quay lại", "Back"), () => ShowStep(2)), Loc.Tr("Bước 3/3", "Step 3/3"));
    }

    private void Finish()
    {
        _s.Prefs.Update(x => x.OnboardingDone = true);
        Close();
    }

    private static Control Layout(Control top, Button primary, Button? secondary, string stepText)
    {
        primary.IsDefault = true;
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 12 };
        bar.Children.Add(primary);
        if (secondary is not null)
        {
            Grid.SetColumn(secondary, 1);
            bar.Children.Add(secondary);
        }
        var step = Ui.Label(stepText, 13, FontWeight.Normal, KV.Muted);
        step.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(step, 3);
        bar.Children.Add(step);

        var dock = new DockPanel();
        DockPanel.SetDock(bar, Dock.Bottom);
        dock.Children.Add(bar);
        dock.Children.Add(top);
        return dock;
    }
}

internal static class DockPanelExtensions
{
    public static DockPanel WithChildren(this DockPanel panel, Control top, Control rest)
    {
        panel.Children.Add(top);
        panel.Children.Add(rest);
        return panel;
    }
}
