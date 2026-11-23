using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace KVietKey.App.UI;

/// <summary>Hộp thoại thông báo đơn giản với một nút OK.</summary>
internal sealed class InfoDialog : Window
{
    public InfoDialog(string title, string message)
    {
        Title = title;
        Width = 460;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = KV.White;
        Icon = AppIcon.Load();

        var ok = Ui.Primary("OK", Close);
        ok.IsDefault = true;
        ok.HorizontalAlignment = HorizontalAlignment.Right;
        ok.MinWidth = 96;
        Content = new Border
        {
            Padding = new Thickness(24),
            Child = Ui.VStack(14, Ui.Label(title, 18, FontWeight.Bold, wrap: true),
                Ui.Label(message, 14, FontWeight.Normal, KV.Text2, wrap: true, lineHeight: 22), ok),
        };
    }
}
