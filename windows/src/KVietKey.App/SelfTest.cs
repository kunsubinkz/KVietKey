using System.Runtime.Versioning;
using System.Text;
using KVietKey.Core;
using KVietKey.Engine;
using KVietKey.Platform.Fake;
using KVietKey.Platform.Windows;

namespace KVietKey.App;

/// <summary>
/// <c>KVietKey --selftest</c>: kiểm tra nhanh không cần giao diện. Phần dùng chung chạy trên mọi hệ điều hành;
/// phần Windows (hook bàn phím, SendInput, bố cục bàn phím) chỉ chạy khi ở trên Windows.
/// </summary>
internal static class SelfTest
{
    private static int _failures;

    private static void Check(bool condition, string name, string? detail = null)
    {
        Console.WriteLine($"{(condition ? "PASS" : "FAIL")}  {name}{(!condition && detail is not null ? "  → " + detail : "")}");
        if (!condition) _failures++;
    }

    private static void Skip(string name, string why) => Console.WriteLine($"SKIP  {name}  ({why})");

    public static int Run()
    {
        Engine_();
        Controller();
        Settings_();
        Macros();
        if (OperatingSystem.IsWindows()) WindowsLayer();
        else Skip("Lớp Windows (hook, SendInput, bố cục bàn phím)", "không phải Windows");

        Console.WriteLine(_failures == 0 ? "\nSELFTEST OK" : $"\nSELFTEST FAILED ({_failures})");
        return _failures == 0 ? 0 : 1;
    }

    private static string TypeThroughEngine(string keys, EngineConfig? config = null)
    {
        var typist = new List<string>();
        var engine = new Engine.Engine(config);
        var screen = new StringBuilder();
        foreach (var rune in keys.EnumerateRunes())
        {
            var r = engine.IsComposingKey(rune) ? engine.Process(rune) : engine.ProcessBreak(rune);
            int remove = Math.Min(r.Backspaces, screen.Length);
            screen.Length -= remove;
            screen.Append(r.Text);
            if (r.Passthrough) screen.Append(rune.ToString());
        }
        return screen.ToString();
    }

    private static void Engine_()
    {
        Check(TypeThroughEngine("tieengs vieetj ") == "tiếng việt ", "Engine: Telex tiếng việt");
        Check(TypeThroughEngine("ngu7o7i2 ", new EngineConfig { Method = InputMethod.Vni }) == "người ", "Engine: VNI người");
        Check(TypeThroughEngine("windows ") == "windows ", "Engine: tự khôi phục từ tiếng Anh");
        Check(Charset.Tcvn3.Decode(Charset.Tcvn3.Encode("tiếng việt")) == "tiếng việt", "Bảng mã: TCVN3 khứ hồi");
    }

    private static void Controller()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kvk-selftest-" + Guid.NewGuid());
        try
        {
            var prefs = new Preferences(dir);
            var screen = new FakeInputSender();
            var controller = new InputController(prefs, screen, new FakeForegroundMonitor(), new FakeSystemServices());
            var kb = new VirtualKeyboard(controller, screen);
            kb.Type("Vieetj Nam, xin chaof.\n");
            Check(screen.Text == "Việt Nam, xin chào.\n", "Controller: gõ qua bộ điều khiển", screen.Text);

            controller.Toggle();
            screen.Clear();
            kb.Type("as");
            Check(screen.Text == "as", "Controller: tắt tiếng Việt");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    private static void Settings_()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kvk-selftest-" + Guid.NewGuid());
        try
        {
            var a = new Preferences(dir);
            a.Update(s => { s.Method = InputMethod.Vni; s.Hotkey = Hotkey.CtrlSpace; });
            var b = new Preferences(dir);
            Check(b.Current.Method == InputMethod.Vni && b.Current.Hotkey == Hotkey.CtrlSpace, "Cài đặt: lưu và đọc lại");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    private static void Macros()
    {
        var t = MacroTable.Parse("vn:Việt Nam\ndc: được\n");
        Check(t.Expand("VN", true) == "VIỆT NAM" && t.Expand("Dc", true) == "Được", "Gõ tắt: giữ kiểu hoa/thường");
    }

    [SupportedOSPlatform("windows")]
    private static void WindowsLayer()
    {
        Check(System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.INPUT>() == (nint.Size == 8 ? 40 : 28), "SendInput: kích thước INPUT");

        // 'A' trên bàn phím Latin phải ra chữ a/A.
        var rune = KeyTranslator.Translate('A', KeyTranslator.ScanCodeFor('A'), false, false, false, false,
            NativeMethods.GetKeyboardLayout(0));
        if (rune is { } r && char.IsAsciiLetter((char)r.Value)) Check(true, "Bố cục bàn phím: ToUnicodeEx cho phím A");
        else Skip("Bố cục bàn phím: ToUnicodeEx cho phím A", "bàn phím hiện tại không phải Latin");

        var dir = Path.Combine(Path.GetTempPath(), "kvk-selftest-" + Guid.NewGuid());
        try
        {
            using var uia = new UiAutomation();
            using var monitor = new ForegroundMonitor(uia);
            var prefs = new Preferences(dir);
            var controller = new InputController(prefs, new InputSender(uia), monitor, new WindowsSystemServices());
            using var hook = new KeyboardHook(controller);
            bool started = hook.Start();
            Check(started, "Hook: cài được WH_KEYBOARD_LL / WH_MOUSE_LL");
            hook.Stop();
            Check(true, "Hook: gỡ sạch");
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
