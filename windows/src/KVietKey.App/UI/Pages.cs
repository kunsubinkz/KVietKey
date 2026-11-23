using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using KVietKey.Core;
using KVietKey.Engine;
using InputMethod = KVietKey.Engine.InputMethod;

namespace KVietKey.App.UI;

/// <summary>Các trang cài đặt. Mỗi hàm dựng một trang và đăng ký việc đồng bộ vào <see cref="Binder"/>.</summary>
internal static class Pages
{
    public static Control Scaffold(string title, string subtitle, Control? trailing, params Control[] content)
    {
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        head.Children.Add(Ui.VStack(4, Ui.Label(title, 26, FontWeight.Bold), Ui.Label(subtitle, 14, FontWeight.Normal, KV.Muted, wrap: true)));
        if (trailing is not null)
        {
            trailing.VerticalAlignment = VerticalAlignment.Top;
            Grid.SetColumn(trailing, 1);
            head.Children.Add(trailing);
        }
        var page = Ui.VStack(16, head);
        page.Children.AddRange(content);
        return page;
    }

    // Cơ bản

    public static string MethodTitle(InputMethod m) => m == InputMethod.Combined ? Loc.Tr("Kết hợp", "Combined") : m.DisplayName();

    public static string MethodExample(InputMethod m) => m switch
    {
        InputMethod.Telex => "aa → â · s → sắc",
        InputMethod.Vni => "a6 → â · 1 → sắc",
        InputMethod.Viqr => "a^ → â · ' → sắc",
        _ => Loc.Tr("Telex + VNI cùng lúc", "Telex + VNI together"),
    };

    public static Control MethodCards(AppServices s, Binder b)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        var items = new List<(InputMethod Method, Button Button, Border Check)>();
        var methods = Enum.GetValues<InputMethod>();
        for (int i = 0; i < methods.Length; i++)
        {
            var m = methods[i];
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            var title = Ui.Label(MethodTitle(m), 16, FontWeight.Bold);
            var example = Ui.Label(MethodExample(m), 13, FontWeight.Normal, KV.Muted, wrap: true);
            var check = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Background = KV.Blue,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Child = new TextBlock { Text = "✓", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = KV.White,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            var inner = new Grid { Margin = new Thickness(14) };
            inner.Children.Add(Ui.VStack(6, title, example));
            inner.Children.Add(check);

            var button = new Button
            {
                Content = inner, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
            };
            button.Classes.Add("k");
            button.Click += (_, _) => s.Prefs.Update(x => x.Method = m);
            Grid.SetColumn(button, i);
            grid.Children.Add(button);
            items.Add((m, button, check));
        }
        b.Add(() =>
        {
            foreach (var (m, button, check) in items)
            {
                bool on = s.Prefs.Current.Method == m;
                button.Background = on ? KV.Soft : KV.White;
                button.BorderBrush = on ? KV.Blue : KV.Border;
                check.IsVisible = on;
            }
        });
        return grid;
    }

    public static IReadOnlyList<(Charset Value, string Label)> CharsetOptions() =>
        Enum.GetValues<Charset>().Select(c => (c, c.DisplayName())).ToList();

    public static Control Basic(AppServices s, Binder b)
    {
        var charset = Ui.Dropdown(b, CharsetOptions(), () => s.Prefs.Current.Charset, v => s.Prefs.Update(x => x.Charset = v));
        var hotkey = new HotkeyRecorder(s, b);

        var cards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 16 };
        var charsetCard = Ui.Card(10, Ui.CardTitle(Loc.Tr("Bảng mã", "Charset")), charset,
            Ui.Hint(Loc.Tr("Còn có: Unicode tổ hợp, TCVN3, VNI Windows, VISCII, CP1258.",
                "Also available: composite Unicode, TCVN3, VNI Windows, VISCII, CP1258.")));
        var hotkeyCard = Ui.Card(10, Ui.CardTitle(Loc.Tr("Phím tắt chuyển Việt ⇄ Anh", "Toggle shortcut Vietnamese ⇄ English")), hotkey,
            Ui.Hint(Loc.Tr("Bấm “Ghi phím mới” rồi nhấn tổ hợp bạn muốn (ít nhất hai phím bổ trợ, hoặc phím bổ trợ + một phím).",
                "Click “Record new shortcut” and press the combination you want (at least two modifiers, or a modifier plus a key).")));
        Grid.SetColumn(hotkeyCard, 1);
        cards.Children.Add(charsetCard);
        cards.Children.Add(hotkeyCard);

        var toggles = Ui.RowsCard(
            Ui.ToggleRow(b, Loc.Tr("Khởi động cùng Windows", "Launch at login"), () => s.Prefs.Current.LaunchAtLogin,
                v => { s.Prefs.Update(x => x.LaunchAtLogin = v); s.System.SetLaunchAtLogin(v); }),
            Ui.ToggleRow(b, Loc.Tr("Phát âm thanh khi chuyển chế độ", "Play a sound when switching"),
                () => s.Prefs.Current.SoundOnToggle, v => s.Prefs.Update(x => x.SoundOnToggle = v)),
            Ui.ToggleRow(b, Loc.Tr("Hiện biểu tượng V / E ở khay hệ thống", "Show V / E icon in the system tray"),
                () => s.Prefs.Current.ShowTrayIcon, v => s.Prefs.Update(x => x.ShowTrayIcon = v), divider: false));

        var content = new List<Control>
        {
            Ui.Card(Ui.VStack(12, Ui.CardTitle(Loc.Tr("Kiểu gõ", "Input method")), MethodCards(s, b))),
            cards,
            toggles,
        };
        if (s.IsFake) content.Add(DemoTypingCard(s));
        return Scaffold(Loc.Tr("Cơ bản", "General"),
            Loc.Tr("Chọn cách gõ và bảng mã bạn quen dùng.", "Choose the typing style and charset you prefer."), null, content.ToArray());
    }

    /// <summary>Chỉ khi chạy không trên Windows: ô gõ thử đi qua đúng bộ điều khiển gõ như hook thật.</summary>
    private static Control DemoTypingCard(AppServices s)
    {
        var output = Ui.Label("", 16, FontWeight.Medium, KV.Ink, wrap: true);
        var box = new Border
        {
            Focusable = true, MinHeight = 72, Padding = new Thickness(14), Background = KV.Bg, CornerRadius = new CornerRadius(12),
            BorderBrush = KV.Border2, BorderThickness = new Thickness(1), Child = output,
        };
        void Refresh() => Ui.OnUi(() => output.Text = s.FakeScreen!.Text + "▏");
        s.FakeScreen!.TextChanged += Refresh;
        Refresh();
        box.TextInput += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Text)) s.FakeKeyboard!.Type(e.Text);
            e.Handled = true;
        };
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Back) { s.FakeKeyboard!.Press(VirtualKeys.Back); e.Handled = true; }
            else if (e.Key == Key.Enter) { s.FakeKeyboard!.Press(VirtualKeys.Return); e.Handled = true; }
        };
        box.PointerPressed += (_, _) => { box.Focus(); s.Controller.OnMouseDown(); };
        return Ui.Card(10, Ui.CardTitle(Loc.Tr("Gõ thử (chế độ giả lập, không phải Windows)", "Try typing (simulated, not Windows)")), box,
            Ui.Hint(Loc.Tr("Bấm vào ô rồi gõ, ví dụ: tieengs vieetj", "Click the box and type, e.g. tieengs vieetj")));
    }

    // Gõ tiếng Việt

    public static Control Typing(AppServices s, Binder b)
    {
        Control Row(string title, Func<Settings, bool> get, Action<Settings, bool> set, string? subtitle = null, bool divider = true) =>
            Ui.ToggleRow(b, title, () => get(s.Prefs.Current), v => s.Prefs.Update(x => set(x, v)), subtitle, divider);

        return Scaffold(Loc.Tr("Gõ tiếng Việt", "Typing"),
            Loc.Tr("Tinh chỉnh cách đặt dấu và kiểm tra chính tả.", "Fine-tune tone placement and spell checking."), null,
            Ui.RowsCard(
                Row(Loc.Tr("Đặt dấu kiểu mới (hoà, thuý)", "Modern tone placement (hoà, thuý)"), x => x.ModernTone, (x, v) => x.ModernTone = v),
                Row(Loc.Tr("Bỏ dấu tự do", "Free marking"), x => x.FreeMarking, (x, v) => x.FreeMarking = v,
                    Loc.Tr("Gõ dấu ở bất kỳ vị trí nào trong từ (tieengs, tungw)", "Apply marks anywhere in the word (tieengs, tungw)"), false)),
            Ui.RowsCard(
                Row(Loc.Tr("Kiểm tra chính tả", "Spell check"), x => x.SpellCheck, (x, v) => x.SpellCheck = v,
                    Loc.Tr("Không biến đổi từ ngoại ngữ như class, show", "Don't transform foreign words like class, show")),
                Row(Loc.Tr("Tự khôi phục từ tiếng Anh", "Auto-restore English words"), x => x.AutoRestore, (x, v) => x.AutoRestore = v,
                    Loc.Tr("Trả lại đúng phím đã gõ khi từ không hợp lệ (windows, text)", "Restore typed keys for invalid words (windows, text)")),
                Row(Loc.Tr("Cho phép phụ âm z, f, w, j", "Allow consonants z, f, w, j"), x => x.AllowZFWJ, (x, v) => x.AllowZFWJ = v),
                Row(Loc.Tr("Phím w đứng một mình thành ư (Telex)", "Standalone w becomes ư (Telex)"), x => x.WAsU, (x, v) => x.WAsU = v, null, false)),
            Ui.RowsCard(
                Row(Loc.Tr("Viết hoa chữ đầu câu", "Capitalize first letter of sentences"), x => x.AutoCapitalize, (x, v) => x.AutoCapitalize = v),
                Row(Loc.Tr("Bật gõ tắt", "Enable macros"), x => x.MacroEnabled, (x, v) => x.MacroEnabled = v, null, false)));
    }

    // Nâng cao

    public static Control Advanced(AppServices s, Binder b)
    {
        var delayLabel = Ui.Label("", 14, FontWeight.Medium);
        var slider = new Slider { Minimum = 0, Maximum = 10000, TickFrequency = 250, IsSnapToTickEnabled = true, SmallChange = 250, LargeChange = 1000 };
        b.Add(() =>
        {
            int v = s.Prefs.Current.ElectronDelayMicros;
            delayLabel.Text = Loc.Tr($"Độ trễ cho ứng dụng Electron (Slack, Discord, VS Code…): {v} µs",
                $"Delay for Electron apps (Slack, Discord, VS Code…): {v} µs");
            slider.Value = v;
        });
        slider.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty || b.Syncing) return;
            int v = (int)slider.Value;
            if (v != s.Prefs.Current.ElectronDelayMicros) s.Prefs.Update(x => x.ElectronDelayMicros = v);
        };

        var compat = Ui.Card(Ui.VStack(0,
            Ui.ToggleRow(b, Loc.Tr("Sửa lỗi gợi ý của trình duyệt", "Fix browser autocomplete"),
                () => s.Prefs.Current.BrowserAutocompleteFix, v => s.Prefs.Update(x => x.BrowserAutocompleteFix = v),
                Loc.Tr("Thanh địa chỉ và ô tìm kiếm của Chrome, Edge, Firefox…", "Address bars and search boxes in Chrome, Edge, Firefox…"), divider: false),
            new Border { Padding = new Thickness(0, 12, 0, 0), Child = Ui.VStack(8, delayLabel, slider) }),
            new Thickness(20, 6, 20, 14));

        var conflicts = Ui.Label("", 13, FontWeight.Medium, KV.Warn, wrap: true);
        var noConflicts = Ui.Hint(Loc.Tr("Không phát hiện bộ gõ khác đang chạy.", "No other input method apps detected."));
        void RefreshConflicts()
        {
            var found = s.System.FindConflictingInputApps();
            conflicts.IsVisible = found.Count > 0;
            noConflicts.IsVisible = found.Count == 0;
            conflicts.Text = Loc.Tr($"Đang chạy bộ gõ khác: {string.Join(", ", found)}. Hãy thoát chúng để tránh xung đột.",
                $"Other input apps running: {string.Join(", ", found)}. Quit them to avoid conflicts.");
        }
        b.Add(RefreshConflicts);

        var diag = Ui.Card(Ui.VStack(12,
            Ui.ToggleRow(b, Loc.Tr("Ghi log debug", "Debug log"), () => s.Prefs.Current.DebugLog, v => s.Prefs.Update(x => x.DebugLog = v),
                Loc.Tr("Chỉ ghi sự kiện, không bao giờ ghi nội dung gõ", "Events only, never typed content"), divider: false),
            conflicts, noConflicts,
            Ui.Hint(Loc.Tr("Nếu dùng bàn phím Vietnamese Telex/VNI có sẵn của Windows, hãy gỡ nó khỏi danh sách bàn phím để tránh gõ trùng.",
                "If you use Windows' built-in Vietnamese Telex/VNI keyboard, remove it from your keyboard list to avoid double processing."))),
            new Thickness(20, 6, 20, 14));

        var language = Ui.Card(10, Ui.CardTitle(Loc.Tr("Ngôn ngữ giao diện", "Interface language")),
            Ui.Segmented(b, [(AppLanguage.Auto, Loc.Tr("Tự động", "Automatic")), (AppLanguage.Vi, "Tiếng Việt"), (AppLanguage.En, "English")],
                () => s.Prefs.Current.Language, v => s.Prefs.Update(x => x.Language = v)));

        var reset = Ui.Outline(Loc.Tr("Khôi phục cài đặt mặc định", "Reset to defaults"), () =>
        {
            var rules = s.Prefs.Current.AppRules.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
            var fresh = new Settings { OnboardingDone = true, AppRules = rules };
            s.Prefs.Update(x =>
            {
                x.Method = fresh.Method; x.Charset = fresh.Charset; x.ModernTone = fresh.ModernTone; x.FreeMarking = fresh.FreeMarking;
                x.SpellCheck = fresh.SpellCheck; x.AutoRestore = fresh.AutoRestore; x.AutoCapitalize = fresh.AutoCapitalize;
                x.AllowZFWJ = fresh.AllowZFWJ; x.WAsU = fresh.WAsU; x.MacroEnabled = fresh.MacroEnabled; x.MacroKeepCase = fresh.MacroKeepCase;
                x.Hotkey = fresh.Hotkey; x.LaunchAtLogin = fresh.LaunchAtLogin; x.SoundOnToggle = fresh.SoundOnToggle;
                x.RememberAppState = fresh.RememberAppState; x.DisableInFullscreen = fresh.DisableInFullscreen;
                x.BrowserAutocompleteFix = fresh.BrowserAutocompleteFix; x.ElectronDelayMicros = fresh.ElectronDelayMicros;
                x.Language = fresh.Language; x.DebugLog = fresh.DebugLog; x.ShowTrayIcon = fresh.ShowTrayIcon;
            });
        });
        reset.HorizontalAlignment = HorizontalAlignment.Left;

        return Scaffold(Loc.Tr("Nâng cao", "Advanced"),
            Loc.Tr("Tương thích theo ứng dụng và công cụ chẩn đoán.", "Per-app compatibility and diagnostics."), null,
            compat, diag, language, reset);
    }

    // Giới thiệu

    public static string Version
    {
        get
        {
            var v = typeof(Pages).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "dev";
            int plus = v.IndexOf('+');
            return plus >= 0 ? v[..plus] : v;
        }
    }

    public static string Architecture => "Windows · " + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    public static Control About()
    {
        return Scaffold(Loc.Tr("Giới thiệu", "About"), Loc.Tr("Bộ gõ tiếng Việt cho Windows.", "Vietnamese input method for Windows."), null,
            Ui.Card(14,
                Ui.HStack(14, Ui.Logo(64),
                    new StackPanel
                    {
                        Spacing = 2, VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            Ui.Label("KVietKey", 22, FontWeight.Bold),
                            Ui.Label($"{Loc.Tr("Phiên bản", "Version")} {Version} · {Architecture}", 13, FontWeight.Normal, KV.Muted),
                        },
                    }),
                Ui.Divider(),
                Ui.Label(Loc.Tr("Riêng tư: KVietKey không lưu, không gửi nội dung bạn gõ và không chứa mã mạng.",
                    "Privacy: KVietKey never stores or sends what you type and contains no network code."), 14, FontWeight.Normal, KV.Text2, wrap: true),
                Ui.Label(Loc.Tr("Được tạo bởi kunsubin", "Created by kunsubin"), 14, FontWeight.SemiBold),
                Ui.Hint(Loc.Tr("© 2026 kunsubin. Phát hành theo giấy phép MIT. Phông Be Vietnam Pro theo giấy phép SIL OFL 1.1.",
                    "© 2026 kunsubin. Released under the MIT license. Be Vietnam Pro font under the SIL OFL 1.1 license."))));
    }
}
