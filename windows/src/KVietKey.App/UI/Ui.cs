using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using KVietKey.Core;

namespace KVietKey.App.UI;

/// <summary>Gom các hàm đồng bộ "cài đặt → điều khiển" để cập nhật giao diện khi cài đặt đổi mà không dựng lại.</summary>
internal sealed class Binder
{
    private readonly List<Action> _refreshers = [];

    /// <summary>Đang đồng bộ từ cài đặt: các handler thay đổi phải bỏ qua để không ghi ngược.</summary>
    public bool Syncing { get; private set; }

    public void Add(Action refresh)
    {
        _refreshers.Add(refresh);
        Run(refresh);
    }

    public void Refresh()
    {
        foreach (var r in _refreshers) Run(r);
    }

    private void Run(Action a)
    {
        bool was = Syncing;
        Syncing = true;
        try { a(); }
        finally { Syncing = was; }
    }
}

/// <summary>Hàm dựng điều khiển theo phong cách KVietKey.</summary>
internal static class Ui
{
    // Văn bản

    public static TextBlock Label(string text, double size = 14, FontWeight weight = FontWeight.Normal,
        IBrush? color = null, bool wrap = false, double lineHeight = 0)
    {
        var t = new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            Foreground = color ?? KV.Ink,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };
        if (lineHeight > 0) t.LineHeight = lineHeight;
        return t;
    }

    public static TextBlock Hint(string text) => Label(text, 12, FontWeight.Normal, KV.Muted, wrap: true);

    public static StackPanel VStack(double spacing = 0, params Control[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Vertical, Spacing = spacing };
        p.Children.AddRange(children);
        return p;
    }

    public static StackPanel HStack(double spacing = 0, params Control[] children)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = spacing };
        p.Children.AddRange(children);
        return p;
    }

    public static Border Rect(IBrush? fill, double radius, double? width = null, double? height = null, Control? child = null,
        IBrush? stroke = null, double strokeWidth = 1)
    {
        var b = new Border
        {
            Background = fill,
            CornerRadius = new CornerRadius(radius),
            Child = child,
        };
        if (stroke is not null) { b.BorderBrush = stroke; b.BorderThickness = new Thickness(strokeWidth); }
        if (width is { } w) b.Width = w;
        if (height is { } h) b.Height = h;
        return b;
    }

    public static Border Divider(IBrush? color = null) => new() { Height = 1, Background = color ?? KV.Soft };

    // Nút

    private static Button Btn(string className, object content, Action onClick, string? tooltip = null)
    {
        var b = new Button { Content = content };
        b.Classes.Add("k");
        b.Classes.Add(className);
        b.Click += (_, _) => onClick();
        if (tooltip is not null) ToolTip.SetTip(b, tooltip);
        return b;
    }

    public static Button Primary(string text, Action onClick, bool big = false)
    {
        var b = Btn("primary", text, onClick);
        if (big) b.Classes.Add("big");
        return b;
    }

    public static Button PrimaryWithIcon(string icon, string text, Action onClick) =>
        Btn("primary", HStack(8, Label(icon, 15, FontWeight.Bold, KV.White), Label(text, 14, FontWeight.SemiBold, KV.White)), onClick);

    public static Button Outline(string text, Action onClick) => Btn("outline", text, onClick);
    public static Button TextButton(string text, Action onClick) => Btn("textbtn", text, onClick);
    public static Button Soft(string text, Action onClick) => Btn("soft", text, onClick);

    /// <summary>Nút biểu tượng vuông; <paramref name="pathData"/> là dữ liệu nét trong <see cref="Icons"/>.</summary>
    public static Button IconButton(string pathData, string tooltip, Action onClick)
    {
        var b = Btn("iconbtn", "", onClick, tooltip);
        b.Content = Icons.Create(pathData, 16, null, b, 1.7);
        Avalonia.Automation.AutomationProperties.SetName(b, tooltip);
        return b;
    }

    // Công tắc

    public static ToggleButton Switch(Binder binder, Func<bool> get, Action<bool> set, string automationName = "")
    {
        var t = new ToggleButton();
        t.Classes.Add("kswitch");
        Avalonia.Automation.AutomationProperties.SetName(t, automationName);
        binder.Add(() => t.IsChecked = get());
        t.IsCheckedChanged += (_, _) =>
        {
            if (binder.Syncing) return;
            set(t.IsChecked == true);
        };
        return t;
    }

    /// <summary>Dòng cài đặt có công tắc, dùng trong thẻ.</summary>
    public static Control ToggleRow(Binder binder, string title, Func<bool> get, Action<bool> set,
        string? subtitle = null, bool divider = true, double minHeight = 52)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Label(title, 14, FontWeight.Medium));
        if (subtitle is not null) text.Children.Add(Hint(subtitle));

        var sw = Switch(binder, get, set, title);
        sw.VerticalAlignment = VerticalAlignment.Center;
        var row = new Grid { MinHeight = minHeight, ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        row.Children.Add(text);
        Grid.SetColumn(sw, 1);
        row.Children.Add(sw);

        var wrap = new StackPanel();
        wrap.Children.Add(row);
        if (divider) wrap.Children.Add(Divider());
        return wrap;
    }

    // Nhóm nút chọn một

    public static Control Segmented<T>(Binder binder, IReadOnlyList<(T Value, string Label)> options, Func<T> get, Action<T> set,
        bool solid = false, double height = 34, double fontSize = 14, double radius = 12) where T : notnull
    {
        var grid = new Grid { ColumnSpacing = 4 };
        var buttons = new List<(T Value, Button Button)>();
        for (int i = 0; i < options.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var (value, label) = options[i];
            var b = new Button { Content = label, Height = height, FontSize = fontSize };
            b.Classes.Add("k");
            b.Classes.Add("seg");
            if (solid) b.Classes.Add("solid");
            b.CornerRadius = new CornerRadius(Math.Max(radius - 3, 4));
            b.Click += (_, _) => { if (!binder.Syncing) set(value); binder.Refresh(); };
            Grid.SetColumn(b, i);
            grid.Children.Add(b);
            buttons.Add((value, b));
        }
        binder.Add(() =>
        {
            var current = get();
            foreach (var (v, b) in buttons) b.Classes.Set("on", EqualityComparer<T>.Default.Equals(v, current));
        });
        return new Border { Background = KV.Soft, CornerRadius = new CornerRadius(radius), Padding = new Thickness(4), Child = grid };
    }

    // Thẻ

    public static Border Card(Control child, Thickness? padding = null)
    {
        var b = new Border { Child = child, Padding = padding ?? new Thickness(20, 18) };
        b.Classes.Add("card");
        return b;
    }

    public static Border Card(double spacing, params Control[] children) => Card(VStack(spacing, children));

    /// <summary>Thẻ chứa các dòng công tắc: đệm dọc nhỏ, không khoảng cách giữa các dòng.</summary>
    public static Border RowsCard(params Control[] rows) => Card(VStack(0, rows), new Thickness(20, 6));

    public static Control CardTitle(string text) => Label(text, 14, FontWeight.SemiBold);

    // Phím tắt hiển thị dạng phím

    public static Control KeyCap(string text, bool large = true)
    {
        var label = Label(text, large ? 15 : 12, FontWeight.SemiBold);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        var b = new Border
        {
            Child = label,
            MinWidth = large ? 40 : 22,
            MinHeight = large ? 36 : 22,
            Padding = new Thickness(large ? 10 : 6, 0),
            Background = large ? KV.White : KV.Bg,
            BorderBrush = KV.KeyBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(large ? 10 : 6),
        };
        if (large) b.BoxShadow = BoxShadows.Parse("0 2 0 0 #C7D3EA");
        return b;
    }

    public static Control KeyCaps(Hotkey hotkey, bool large = true)
    {
        var p = HStack(4);
        foreach (var part in hotkey.DisplayString.Split(" + ")) p.Children.Add(KeyCap(part, large));
        return p;
    }

    // Logo

    public static Control Logo(double size = 40) => new LogoControl { Width = size, Height = size };

    // Dropdown dạng nút + menu

    public static Control Dropdown<T>(Binder binder, IReadOnlyList<(T Value, string Label)> options, Func<T> get, Action<T> set,
        double height = 40, double? width = null, IBrush? background = null) where T : notnull
    {
        var label = Label("", 14, FontWeight.SemiBold, KV.Blue);
        var chevron = Label("⌄", 14, FontWeight.Bold, KV.Muted);
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        content.Children.Add(label);
        Grid.SetColumn(chevron, 1);
        content.Children.Add(chevron);

        var button = new Button { Content = content, Height = height, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        button.Classes.Add("k");
        button.Classes.Add("outline");
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.Padding = new Thickness(14, 0);
        if (width is { } w) button.Width = w;
        if (background is not null) button.Background = background;

        var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };
        foreach (var (value, text) in options)
        {
            var item = new MenuItem { Header = text };
            item.Click += (_, _) => { set(value); binder.Refresh(); };
            flyout.Items.Add(item);
        }
        button.Flyout = flyout;
        binder.Add(() => label.Text = options.FirstOrDefault(o => EqualityComparer<T>.Default.Equals(o.Value, get())).Label ?? "");
        return button;
    }

    public static void OnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }
}

/// <summary>Logo KVietKey: nền xanh bo góc, chữ V trắng và nét dấu màu ngọc.</summary>
internal sealed class LogoControl : Control
{
    public override void Render(DrawingContext context)
    {
        double s = Bounds.Width / 40;
        context.DrawRectangle(new ImmutableSolidColorBrush(Color.Parse("#1F5BFF")), null,
            new RoundedRect(new Rect(0, 0, Bounds.Width, Bounds.Height), 11 * s));

        var v = new StreamGeometry();
        using (var g = v.Open())
        {
            g.BeginFigure(new Point(12 * s, 15 * s), false);
            g.LineTo(new Point(20 * s, 29 * s));
            g.LineTo(new Point(28 * s, 15 * s));
            g.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(Brushes.White, 3.6 * s, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), v);

        var mark = new StreamGeometry();
        using (var g = mark.Open())
        {
            g.BeginFigure(new Point(24 * s, 9 * s), false);
            g.LineTo(new Point(28.5 * s, 6.5 * s));
            g.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(new ImmutableSolidColorBrush(Color.Parse("#7DE3F5")), 2.8 * s, lineCap: PenLineCap.Round), mark);
    }
}
