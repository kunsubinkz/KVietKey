using KVietKey.Core;
using KVietKey.Platform.Fake;
using KVietKey.Platform.Windows;

namespace KVietKey.App;

/// <summary>Các thành phần dùng chung của ứng dụng: cài đặt, bộ điều khiển gõ, nền tảng (thật hoặc giả lập).</summary>
internal sealed class AppServices : IDisposable
{
    public required Preferences Prefs { get; init; }
    public required InputController Controller { get; init; }
    public required ISystemServices System { get; init; }

    /// <summary>Chạy không trên Windows (xem trước giao diện, kiểm thử): không có hook bàn phím thật.</summary>
    public bool IsFake { get; init; }

    public FakeInputSender? FakeScreen { get; init; }
    public VirtualKeyboard? FakeKeyboard { get; init; }
    public FakeForegroundMonitor? FakeForeground { get; init; }

    private KeyboardHook? Hook { get; init; }
    private IDisposable[] Disposables { get; init; } = [];

    /// <summary>Mở cửa sổ cài đặt ở trang chỉ định (do AppHost gán).</summary>
    public Action<UI.SettingsTab> OpenSettings { get; set; } = _ => { };

    public Action Quit { get; set; } = () => { };

    /// <summary>Vị trí con trỏ chuột trên màn hình (pixel) để đặt panel gần khay; null nếu không biết.</summary>
    public Func<(int X, int Y)?> CursorPosition { get; set; } = () => null;

    public static AppServices Create(string dataDirectory, bool forceFake = false)
    {
        var prefs = new Preferences(dataDirectory);

        if (OperatingSystem.IsWindows() && !forceFake)
        {
            var uia = new UiAutomation();
            var monitor = new ForegroundMonitor(uia);
            monitor.Start();
            var sender = new InputSender(uia);
            var system = new WindowsSystemServices();
            var controller = new InputController(prefs, sender, monitor, system);
            return new AppServices
            {
                Prefs = prefs,
                Controller = controller,
                System = system,
                Hook = new KeyboardHook(controller),
                Disposables = [monitor, uia],
                CursorPosition = WindowsCursor.TryGet,
            };
        }

        var screen = new FakeInputSender();
        var foreground = new FakeForegroundMonitor();
        var fakeSystem = new FakeSystemServices();
        var fakeController = new InputController(prefs, screen, foreground, fakeSystem);
        return new AppServices
        {
            Prefs = prefs,
            Controller = fakeController,
            System = fakeSystem,
            IsFake = true,
            FakeScreen = screen,
            FakeKeyboard = new VirtualKeyboard(fakeController, screen),
            FakeForeground = foreground,
        };
    }

    /// <summary>Bắt đầu bắt phím. Trả về false nếu Windows từ chối cài hook.</summary>
    public bool StartInput() => OperatingSystem.IsWindows() ? Hook?.Start() ?? true : true;

    public void StopInput()
    {
        if (OperatingSystem.IsWindows()) Hook?.Stop();
        Controller.Stop();
    }

    public void Dispose()
    {
        StopInput();
        foreach (var d in Disposables) d.Dispose();
    }
}
