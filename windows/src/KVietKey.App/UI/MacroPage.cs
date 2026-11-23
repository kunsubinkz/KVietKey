using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using KVietKey.Core;
using KVietKey.Engine;

namespace KVietKey.App.UI;

/// <summary>Trang "Gõ tắt": bảng viết tắt → nội dung, nhập/xuất .txt.</summary>
internal static class MacroPage
{
    public static Control Create(AppServices s, Binder b)
    {
        var search = new TextBox();
        search.Classes.Add("kfield");
        var rows = new StackPanel();

        void Rebuild()
        {
            var table = s.Prefs.Macros;
            search.Watermark = Loc.Tr($"Tìm trong {table.Entries.Count} mục gõ tắt", $"Search {table.Entries.Count} macros");
            rows.Children.Clear();
            var q = search.Text?.Trim() ?? "";
            var list = table.Entries
                .Where(e => q.Length == 0 || e.Key.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                    || e.Value.Contains(q, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(e => e.Key, StringComparer.CurrentCultureIgnoreCase).ToList();
            if (list.Count == 0)
            {
                var empty = Ui.Label(Loc.Tr("Chưa có mục gõ tắt nào.", "No macros yet."), 13, FontWeight.Normal, KV.Muted);
                empty.HorizontalAlignment = HorizontalAlignment.Center;
                empty.Margin = new Thickness(0, 30);
                rows.Children.Add(empty);
            }
            for (int i = 0; i < list.Count; i++)
            {
                rows.Children.Add(Row(s, rows, list[i].Key, list[i].Value));
                if (i < list.Count - 1) rows.Children.Add(Ui.Divider());
            }
        }
        search.TextChanged += (_, _) => Rebuild();
        b.Add(Rebuild);
        Action onMacrosChanged = () => Ui.OnUi(Rebuild);
        s.Prefs.MacrosChanged += onMacrosChanged;
        rows.DetachedFromVisualTree += (_, _) => s.Prefs.MacrosChanged -= onMacrosChanged;

        var add = Ui.PrimaryWithIcon("+", Loc.Tr("Thêm mục", "Add"), () => Edit(s, rows, null, "", ""));

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*,76"), Height = 38 };
        var h1 = Ui.Label(Loc.Tr("Viết tắt", "Abbreviation"), 12, FontWeight.SemiBold, KV.Muted);
        var h2 = Ui.Label(Loc.Tr("Mở rộng thành", "Expands to"), 12, FontWeight.SemiBold, KV.Muted);
        h1.VerticalAlignment = h2.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(h2, 1);
        header.Children.Add(h1);
        header.Children.Add(h2);
        var table = Ui.Card(Ui.VStack(0, header, Ui.Divider(), rows), new Thickness(20, 0));

        var keepCase = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        var kcText = Ui.VStack(2, Ui.Label(Loc.Tr("Giữ kiểu hoa / thường", "Keep letter case"), 14, FontWeight.SemiBold),
            Ui.Label("vn → Việt Nam · VN → VIỆT NAM", 12, FontWeight.Normal, KV.Text2));
        kcText.VerticalAlignment = VerticalAlignment.Center;
        var kcSwitch = Ui.Switch(b, () => s.Prefs.Current.MacroKeepCase, v => s.Prefs.Update(x => x.MacroKeepCase = v),
            Loc.Tr("Giữ kiểu hoa / thường", "Keep letter case"));
        kcSwitch.VerticalAlignment = VerticalAlignment.Center;
        keepCase.Children.Add(kcText);
        Grid.SetColumn(kcSwitch, 1);
        keepCase.Children.Add(kcSwitch);
        var banner = new Border { Child = keepCase, Background = KV.Banner, CornerRadius = new CornerRadius(14), Padding = new Thickness(16, 12) };

        var import = Ui.Outline(Loc.Tr("Nhập .txt", "Import .txt"), async () => await ImportFile(s, rows));
        var export = Ui.Outline(Loc.Tr("Xuất .txt", "Export .txt"), async () => await ExportFile(s, rows));

        var topBar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        topBar.Children.Add(search);
        Grid.SetColumn(add, 1);
        topBar.Children.Add(add);

        return Pages.Scaffold(Loc.Tr("Gõ tắt", "Macros"),
            Loc.Tr("Gõ từ viết tắt rồi nhấn cách để mở rộng thành cả cụm.", "Type an abbreviation then press space to expand it."),
            Ui.HStack(8, import, export), topBar, table, banner);
    }

    private static Control Row(AppServices s, Control anchor, string key, string value)
    {
        var chip = Ui.Rect(KV.Soft2, 8, child: new TextBlock
        {
            Text = key, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = KV.Blue, Margin = new Thickness(10, 4),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        chip.HorizontalAlignment = HorizontalAlignment.Left;
        chip.VerticalAlignment = VerticalAlignment.Center;
        chip.MaxWidth = 120;

        var text = Ui.Label(value, 14, FontWeight.Medium);
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.VerticalAlignment = VerticalAlignment.Center;

        var edit = Ui.IconButton(Icons.Pencil, Loc.Tr($"Sửa {key}", $"Edit {key}"), () => Edit(s, anchor, key, key, value));
        var del = Ui.IconButton(Icons.Trash, Loc.Tr($"Xóa {key}", $"Delete {key}"), () =>
        {
            var table = s.Prefs.Macros.Clone();
            table.Entries.Remove(key);
            s.Prefs.SetMacros(table);
        });
        var buttons = Ui.HStack(4, edit, del);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        buttons.VerticalAlignment = VerticalAlignment.Center;

        var g = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*,76"), Height = 50 };
        g.Children.Add(chip);
        Grid.SetColumn(text, 1);
        g.Children.Add(text);
        Grid.SetColumn(buttons, 2);
        g.Children.Add(buttons);
        return g;
    }

    private static async void Edit(AppServices s, Control anchor, string? originalKey, string key, string value)
    {
        if (TopLevel.GetTopLevel(anchor) is not Window owner) return;
        var result = await new MacroEditDialog(originalKey is null, key, value).ShowDialog<(string Key, string Value)?>(owner);
        if (result is not { } r) return;
        var table = s.Prefs.Macros.Clone();
        if (originalKey is not null && originalKey != r.Key) table.Entries.Remove(originalKey);
        table.Entries[r.Key] = r.Value;
        s.Prefs.SetMacros(table);
    }

    private static async Task ImportFile(AppServices s, Control anchor)
    {
        if (TopLevel.GetTopLevel(anchor) is not { } top) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Text") { Patterns = ["*.txt"] }],
        });
        if (files.Count == 0) return;
        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        var imported = MacroTable.Parse(await reader.ReadToEndAsync());
        var merged = s.Prefs.Macros.Clone();
        merged.Merge(imported);
        s.Prefs.SetMacros(merged);
    }

    private static async Task ExportFile(AppServices s, Control anchor)
    {
        if (TopLevel.GetTopLevel(anchor) is not { } top) return;
        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = "kvietkey-macros.txt",
            DefaultExtension = "txt",
            FileTypeChoices = [new FilePickerFileType("Text") { Patterns = ["*.txt"] }],
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        await using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        await writer.WriteAsync(s.Prefs.Macros.ExportText());
    }
}

internal sealed class MacroEditDialog : Window
{
    public MacroEditDialog(bool isNew, string key, string value)
    {
        Title = isNew ? Loc.Tr("Thêm gõ tắt", "Add macro") : Loc.Tr("Sửa gõ tắt", "Edit macro");
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = KV.White;

        var keyBox = new TextBox { Text = key, Watermark = "vn" };
        var valueBox = new TextBox { Text = value, Watermark = "Việt Nam" };
        keyBox.Classes.Add("kfield");
        valueBox.Classes.Add("kfield");

        var save = Ui.Primary(Loc.Tr("Lưu", "Save"), () => Close((keyBox.Text!.Trim(), valueBox.Text!)));
        void Validate() => save.IsEnabled = !string.IsNullOrWhiteSpace(keyBox.Text) && !string.IsNullOrEmpty(valueBox.Text);
        keyBox.TextChanged += (_, _) => Validate();
        valueBox.TextChanged += (_, _) => Validate();
        Validate();
        save.IsDefault = true;
        var cancel = Ui.TextButton(Loc.Tr("Hủy", "Cancel"), () => Close(null));
        cancel.IsCancel = true;

        var buttons = Ui.HStack(8, cancel, save);
        buttons.HorizontalAlignment = HorizontalAlignment.Right;
        Content = new Border
        {
            Padding = new Thickness(24),
            Child = Ui.VStack(14,
                Ui.Label(Title, 18, FontWeight.Bold),
                Ui.VStack(6, Ui.Label(Loc.Tr("Viết tắt", "Abbreviation"), 12, FontWeight.SemiBold, KV.Muted), keyBox),
                Ui.VStack(6, Ui.Label(Loc.Tr("Mở rộng thành", "Expands to"), 12, FontWeight.SemiBold, KV.Muted), valueBox),
                buttons),
        };
    }
}
