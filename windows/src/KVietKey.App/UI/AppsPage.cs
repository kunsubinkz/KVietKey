using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using KVietKey.Core;
using KVietKey.Engine;

namespace KVietKey.App.UI;

internal static class AppInfo
{
    /// <summary>"chrome.exe" → "Chrome".</summary>
    public static string DisplayName(string exe)
    {
        var name = exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? exe[..^4] : exe;
        return name.Length == 0 ? exe : char.ToUpperInvariant(name[0]) + name[1..];
    }
}

/// <summary>Trang "Ứng dụng": chế độ gõ riêng cho từng ứng dụng.</summary>
internal static class AppsPage
{
    public static Control Create(AppServices s, Binder b)
    {
        var search = new TextBox { Watermark = Loc.Tr("Tìm ứng dụng hoặc tên file .exe", "Search apps or .exe name") };
        search.Classes.Add("kfield");
        var rows = new StackPanel();
        var table = BuildTable(rows);

        void Rebuild()
        {
            rows.Children.Clear();
            var q = search.Text?.Trim() ?? "";
            var list = s.Prefs.Current.AppRules
                .Where(r => q.Length == 0 || r.Key.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || AppInfo.DisplayName(r.Key).Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => AppInfo.DisplayName(r.Key), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (list.Count == 0)
            {
                var empty = Ui.Label(Loc.Tr("Chưa có ứng dụng nào. Bấm “Thêm ứng dụng” để đặt chế độ riêng.",
                    "No apps yet. Click “Add app” to set a dedicated mode."), 13, FontWeight.Normal, KV.Muted);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.Margin = new Thickness(0, 30);
                rows.Children.Add(empty);
            }
            for (int i = 0; i < list.Count; i++)
            {
                rows.Children.Add(Row(s, list[i].Key, list[i].Value, i));
                if (i < list.Count - 1) rows.Children.Add(Ui.Divider());
            }
        }
        search.TextChanged += (_, _) => Rebuild();
        b.Add(Rebuild);

        var add = Ui.PrimaryWithIcon("+", Loc.Tr("Thêm ứng dụng", "Add app"), async () =>
        {
            var owner = TopLevel.GetTopLevel(rows) as Window;
            if (owner is null) return;
            var exe = await new AddAppDialog().ShowDialog<string?>(owner);
            if (exe is null) return;
            s.Prefs.Update(x => { if (!x.AppRules.ContainsKey(exe)) x.AppRules[exe] = new AppRule { Mode = AppMode.English }; });
        });

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        top.Children.Add(search);
        Grid.SetColumn(add, 1);
        top.Children.Add(add);

        Control Option(string title, Func<bool> get, Action<bool> set)
        {
            var label = Ui.Label(title, 13, FontWeight.Medium, KV.Ink, wrap: true);
            var sw = Ui.Switch(b, get, set, title);
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8, MinHeight = 52 };
            label.VerticalAlignment = VerticalAlignment.Center;
            sw.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(label);
            Grid.SetColumn(sw, 1);
            g.Children.Add(sw);
            return new Border { Child = g, Padding = new Thickness(16, 0), Background = KV.White, CornerRadius = new CornerRadius(14),
                BorderBrush = KV.Border, BorderThickness = new Thickness(1) };
        }

        var options = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12 };
        options.Children.Add(Option(Loc.Tr("Nhớ trạng thái cuối theo từng ứng dụng", "Remember last state per app"),
            () => s.Prefs.Current.RememberAppState, v => s.Prefs.Update(x => x.RememberAppState = v)));
        var fs = Option(Loc.Tr("Tự tắt khi ứng dụng toàn màn hình", "Pause for full-screen apps"),
            () => s.Prefs.Current.DisableInFullscreen, v => s.Prefs.Update(x => x.DisableInFullscreen = v));
        Grid.SetColumn(fs, 1);
        options.Children.Add(fs);

        return Pages.Scaffold(Loc.Tr("Ứng dụng", "Apps"),
            Loc.Tr("Mỗi ứng dụng một chế độ riêng, tự áp dụng khi bạn chuyển cửa sổ.",
                "A separate mode per app, applied automatically when you switch windows."), null,
            top, options, table);
    }

    private static Control BuildTable(StackPanel rows)
    {
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,190,150,28"), Height = 38 };
        void Add(string text, int col)
        {
            var l = Ui.Label(text, 12, FontWeight.SemiBold, KV.Muted);
            l.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(l, col);
            header.Children.Add(l);
        }
        Add(Loc.Tr("Ứng dụng", "App"), 0);
        Add(Loc.Tr("Chế độ", "Mode"), 1);
        Add(Loc.Tr("Bảng mã", "Charset"), 2);
        return Ui.Card(Ui.VStack(0, header, Ui.Divider(), rows), new Thickness(20, 0));
    }

    private static Control Row(AppServices s, string exe, AppRule rule, int index)
    {
        var name = AppInfo.DisplayName(exe);
        var (tileBg, tileFg) = KV.Tiles[index % KV.Tiles.Length];
        var tile = Ui.Rect(tileBg, 10, 34, 34, new TextBlock
        {
            Text = name[..1].ToUpperInvariant(), FontSize = 15, FontWeight = FontWeight.Bold, Foreground = tileFg,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        });
        var title = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center,
            Children = { Ui.Label(name, 14, FontWeight.SemiBold), Ui.Label(exe, 12, FontWeight.Normal, KV.Muted) } };
        var left = Ui.HStack(12, tile, title);
        left.VerticalAlignment = VerticalAlignment.Center;

        void Update(Func<AppRule, AppRule> change) =>
            s.Prefs.Update(x => x.AppRules[exe] = change(x.AppRules.TryGetValue(exe, out var r) ? r : rule));

        var local = new Binder();
        AppMode Mode() => (s.Prefs.Current.AppRules.TryGetValue(exe, out var r) ? r : rule).Mode is var m && m == AppMode.Auto ? AppMode.Vietnamese : m;
        var mode = Ui.Segmented(local, [(AppMode.Vietnamese, Loc.Tr("Việt", "VI")), (AppMode.English, Loc.Tr("Anh", "EN")), (AppMode.Off, Loc.Tr("Tắt", "Off"))],
            Mode, v => Update(r => r with { Mode = v }), height: 28, fontSize: 12, radius: 10);
        mode.Margin = new Thickness(0, 0, 12, 0);
        mode.VerticalAlignment = VerticalAlignment.Center;

        var charsetOptions = new List<(string Value, string Label)> { ("", Loc.Tr("Mặc định", "Default")) };
        charsetOptions.AddRange(Enum.GetValues<Charset>().Select(c => (c.ToString(), c == Charset.Unicode ? "Unicode" : c.DisplayName())));
        var charset = Ui.Dropdown(local, charsetOptions,
            () => (s.Prefs.Current.AppRules.TryGetValue(exe, out var r) ? r : rule).Charset?.ToString() ?? "",
            v => Update(r => r with { Charset = v.Length == 0 ? null : Enum.Parse<Charset>(v) }), height: 32);
        charset.VerticalAlignment = VerticalAlignment.Center;

        var remove = Ui.IconButton(Icons.Trash, Loc.Tr("Xóa khỏi danh sách", "Remove from list"),
            () => s.Prefs.Update(x => x.AppRules.Remove(exe)));
        remove.VerticalAlignment = VerticalAlignment.Center;

        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,190,150,28"), Height = 56 };
        g.Children.Add(left);
        Grid.SetColumn(mode, 1);
        g.Children.Add(mode);
        Grid.SetColumn(charset, 2);
        g.Children.Add(charset);
        Grid.SetColumn(remove, 3);
        g.Children.Add(remove);
        return g;
    }
}

/// <summary>Chọn ứng dụng từ danh sách đang chạy hoặc chọn file .exe.</summary>
internal sealed class AddAppDialog : Window
{
    private readonly ListBox _list = new() { Background = Brushes.Transparent, MaxHeight = 320 };
    private readonly TextBox _search = new() { Watermark = Loc.Tr("Tìm trong ứng dụng đang chạy", "Search running apps") };
    private readonly List<(string Exe, string Title)> _all = [];

    public AddAppDialog()
    {
        Title = Loc.Tr("Thêm ứng dụng", "Add app");
        Width = 480;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = KV.White;

        _search.Classes.Add("kfield");
        _search.TextChanged += (_, _) => Fill();
        LoadRunning();
        Fill();
        _list.DoubleTapped += (_, _) => Choose();

        var browse = Ui.Outline(Loc.Tr("Chọn file .exe…", "Browse for .exe…"), async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Ứng dụng") { Patterns = ["*.exe"] }],
            });
            if (files.Count > 0) Close(Path.GetFileName(files[0].Path.LocalPath).ToLowerInvariant());
        });
        var add = Ui.Primary(Loc.Tr("Thêm", "Add"), Choose);
        var cancel = Ui.TextButton(Loc.Tr("Hủy", "Cancel"), () => Close(null));
        var buttons = Ui.HStack(8, browse);
        var right = Ui.HStack(8, cancel, add);
        right.HorizontalAlignment = HorizontalAlignment.Right;
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        bar.Children.Add(buttons);
        Grid.SetColumn(right, 1);
        bar.Children.Add(right);

        Content = new Border
        {
            Padding = new Thickness(24),
            Child = Ui.VStack(14, Ui.Label(Loc.Tr("Thêm ứng dụng", "Add app"), 18, FontWeight.Bold), _search, _list, bar),
        };
    }

    private void LoadRunning()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                bool visible = !OperatingSystem.IsWindows() || (p.MainWindowHandle != 0 && p.MainWindowTitle.Length > 0);
                if (!visible) continue;
                var exe = p.ProcessName.ToLowerInvariant() + ".exe";
                if (seen.Add(exe)) _all.Add((exe, p.MainWindowTitle));
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // Process đã thoát hoặc không đọc được: bỏ qua.
            }
            finally
            {
                p.Dispose();
            }
        }
        _all.Sort((a, b) => string.Compare(a.Exe, b.Exe, StringComparison.OrdinalIgnoreCase));
    }

    private void Fill()
    {
        var q = _search.Text?.Trim() ?? "";
        _list.ItemsSource = _all
            .Where(a => q.Length == 0 || a.Exe.Contains(q, StringComparison.OrdinalIgnoreCase) || a.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(a => new ListBoxItem
            {
                Tag = a.Exe,
                Content = Ui.VStack(0, Ui.Label(AppInfo.DisplayName(a.Exe) + "  ·  " + a.Exe, 13, FontWeight.SemiBold),
                    Ui.Label(a.Title, 12, FontWeight.Normal, KV.Muted)),
            }).ToList();
    }

    private void Choose()
    {
        if (_list.SelectedItem is ListBoxItem { Tag: string exe }) Close(exe);
    }
}
