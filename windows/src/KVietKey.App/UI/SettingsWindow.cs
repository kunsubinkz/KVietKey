using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using KVietKey.Core;

namespace KVietKey.App.UI;

internal enum SettingsTab { Basic, Typing, Apps, Macros, Advanced, About }

internal static class SettingsTabInfo
{
    public static string Title(SettingsTab t) => t switch
    {
        SettingsTab.Basic => Loc.Tr("Cơ bản", "General"),
        SettingsTab.Typing => Loc.Tr("Gõ tiếng Việt", "Typing"),
        SettingsTab.Apps => Loc.Tr("Ứng dụng", "Apps"),
        SettingsTab.Macros => Loc.Tr("Gõ tắt", "Macros"),
        SettingsTab.Advanced => Loc.Tr("Nâng cao", "Advanced"),
        _ => Loc.Tr("Giới thiệu", "About"),
    };

    public static string Icon(SettingsTab t) => t switch
    {
        SettingsTab.Basic => Icons.Sliders,
        SettingsTab.Typing => Icons.Type,
        SettingsTab.Apps => Icons.Grid,
        SettingsTab.Macros => Icons.Bolt,
        SettingsTab.Advanced => Icons.Shield,
        _ => Icons.Info,
    };
}

/// <summary>Cửa sổ cài đặt 6 mục: sidebar navy bên trái, nội dung bên phải.</summary>
internal sealed class SettingsWindow : Window
{
    private readonly AppServices _s;
    private readonly ContentControl _page = new();
    private readonly List<(SettingsTab Tab, Button Button)> _nav = [];
    private Binder _binder = new();
    private SettingsTab _tab = SettingsTab.Basic;
    private AppLanguage _builtLanguage;

    public SettingsWindow(AppServices services)
    {
        _s = services;
        Title = "KVietKey";
        Width = 960;
        Height = 680;
        MinWidth = 880;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Icon = AppIcon.Load();
        Background = KV.Bg;

        Build();
        _s.Prefs.SettingsChanged += OnSettingsChanged;
        Closed += (_, _) => _s.Prefs.SettingsChanged -= OnSettingsChanged;
    }

    public SettingsTab CurrentTab => _tab;

    public void ShowTab(SettingsTab tab)
    {
        SelectTab(tab);
        if (!IsVisible) Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private void OnSettingsChanged(Settings old, Settings updated) => Ui.OnUi(() =>
    {
        if (updated.Language != _builtLanguage)
        {
            Build();
            return;
        }
        _binder.Refresh();
    });

    private void Build()
    {
        _builtLanguage = _s.Prefs.Current.Language;
        Title = "KVietKey";
        _nav.Clear();

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*") };
        grid.Children.Add(Sidebar());

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new Border { Padding = new Thickness(32), Child = _page },
        };
        Grid.SetColumn(scroll, 1);
        grid.Children.Add(scroll);
        Content = grid;
        SelectTab(_tab);
    }

    private Control Sidebar()
    {
        var items = new StackPanel { Spacing = 4 };
        foreach (var tab in Enum.GetValues<SettingsTab>())
        {
            var button = new Button { HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Classes.Add("k");
            button.Classes.Add("nav");
            var text = Ui.Label(SettingsTabInfo.Title(tab), 14, FontWeight.SemiBold);
            text.ClearValue(TextBlock.ForegroundProperty);
            text.VerticalAlignment = VerticalAlignment.Center;
            var icon = Icons.Create(SettingsTabInfo.Icon(tab), 18, null, button);
            icon.VerticalAlignment = VerticalAlignment.Center;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { icon, text } };
            button.Content = row;
            button.Click += (_, _) => SelectTab(tab);
            items.Children.Add(button);
            _nav.Add((tab, button));
        }

        var version = Ui.VStack(2,
            Ui.Label($"{Loc.Tr("Phiên bản", "Version")} {Pages.Version}", 12, FontWeight.SemiBold, KV.White),
            Ui.Label(Pages.Architecture, 12, FontWeight.Normal, KV.NavyText));
        var versionCard = new Border { Child = version, Padding = new Thickness(12), CornerRadius = new CornerRadius(14), Background = KV.White08 };

        var logo = Ui.HStack(10, Ui.Logo(36), Ui.Label("KVietKey", 17, FontWeight.Bold, KV.White));
        logo.Margin = new Thickness(8, 12, 0, 0);
        ((TextBlock)logo.Children[1]).VerticalAlignment = VerticalAlignment.Center;

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(versionCard, Dock.Bottom);
        layout.Children.Add(versionCard);
        var top = Ui.VStack(20, logo, items);
        layout.Children.Add(top);
        return new Border { Background = KV.Navy, Padding = new Thickness(16, 20), Child = layout };
    }

    private void SelectTab(SettingsTab tab)
    {
        _tab = tab;
        _binder = new Binder();
        foreach (var (t, b) in _nav) b.Classes.Set("on", t == tab);
        _page.Content = tab switch
        {
            SettingsTab.Basic => Pages.Basic(_s, _binder),
            SettingsTab.Typing => Pages.Typing(_s, _binder),
            SettingsTab.Apps => AppsPage.Create(_s, _binder),
            SettingsTab.Macros => MacroPage.Create(_s, _binder),
            SettingsTab.Advanced => Pages.Advanced(_s, _binder),
            _ => Pages.About(),
        };
    }
}

/// <summary>Icon ứng dụng dùng cho cửa sổ.</summary>
internal static class AppIcon
{
    private static WindowIcon? _icon;

    public static WindowIcon Load()
    {
        if (_icon is not null) return _icon;
        using var stream = AssetLoader.Open(new Uri("avares://KVietKey/Assets/kvietkey.png"));
        return _icon = new WindowIcon(new Bitmap(stream));
    }
}
