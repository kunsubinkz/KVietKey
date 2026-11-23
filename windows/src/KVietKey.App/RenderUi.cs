using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using KVietKey.App.UI;
using KVietKey.Core;

namespace KVietKey.App;

/// <summary>
/// <c>KVietKey --render-ui &lt;thư mục&gt;</c>: xuất ảnh PNG các màn hình để đối chiếu với bản thiết kế
/// (chạy được trên macOS/Linux, không cần Windows hay màn hình).
/// </summary>
internal static class RenderUi
{
    public static int Run(string outputDirectory)
    {
        AppRuntime.RenderOnly = true;
        Directory.CreateDirectory(outputDirectory);
        var data = Path.Combine(Path.GetTempPath(), "kvk-render-" + Guid.NewGuid());

        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .With(new FontManagerOptions { DefaultFamilyName = KV.FontUri })
            .SetupWithoutStarting();

        int failures = 0;
        try
        {
            var vi = AppServices.Create(Path.Combine(data, "vi"), forceFake: true);
            vi.Prefs.Update(s => { s.Language = AppLanguage.Vi; s.OnboardingDone = true; });
            Seed(vi);
            foreach (var tab in Enum.GetValues<SettingsTab>())
                failures += Capture(outputDirectory, $"settings-{tab.ToString().ToLowerInvariant()}", () => Prepare(new SettingsWindow(vi), w => w.ShowTab(tab)));
            failures += Capture(outputDirectory, "menu-panel-vi", () => Prepare(new MenuPanelWindow(vi), w => w.ShowNear()));
            failures += Capture(outputDirectory, "onboarding-1", () => Prepare(new OnboardingWindow(vi, 1), _ => { }));
            failures += Capture(outputDirectory, "onboarding-2", () => Prepare(new OnboardingWindow(vi, 2), _ => { }));
            failures += Capture(outputDirectory, "onboarding-3", () => Prepare(new OnboardingWindow(vi, 3), _ => { }));

            vi.Controller.SetVietnamese(false);
            failures += Capture(outputDirectory, "menu-panel-en-off", () => Prepare(new MenuPanelWindow(vi), w => w.ShowNear()));

            var en = AppServices.Create(Path.Combine(data, "en"), forceFake: true);
            en.Prefs.Update(s => { s.Language = AppLanguage.En; s.OnboardingDone = true; });
            failures += Capture(outputDirectory, "settings-basic-en", () => Prepare(new SettingsWindow(en), w => w.ShowTab(SettingsTab.Basic)));

            foreach (var (name, vietnamese) in new[] { ("tray-v", true), ("tray-e", false) })
            {
                using var bmp = TrayController.RenderChip(vietnamese ? "V" : "E", vietnamese, paused: false, 128);
                bmp.Save(Path.Combine(outputDirectory, name + ".png"));
                Console.WriteLine($"OK    {name}.png");
            }
        }
        finally
        {
            if (Directory.Exists(data)) Directory.Delete(data, true);
        }
        return failures == 0 ? 0 : 1;
    }

    private static void Seed(AppServices s)
    {
        s.Prefs.Update(x =>
        {
            x.AppRules["chrome.exe"] = new AppRule { Mode = AppMode.Vietnamese };
            x.AppRules["code.exe"] = new AppRule { Mode = AppMode.English, Charset = Engine.Charset.Tcvn3 };
            x.AppRules["cmd.exe"] = new AppRule { Mode = AppMode.Off };
        });
        s.Prefs.SetMacros(new Engine.MacroTable(new Dictionary<string, string>
        {
            ["vn"] = "Việt Nam", ["hcm"] = "Hồ Chí Minh", ["dc"] = "được", ["ko"] = "không", ["tks"] = "cảm ơn bạn",
        }));
    }

    private static T Prepare<T>(T window, Action<T> show) where T : Window
    {
        show(window);
        if (!window.IsVisible) window.Show();
        return window;
    }

    private static int Capture(string directory, string name, Func<Window> create)
    {
        try
        {
            var window = create();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            var frame = window.CaptureRenderedFrame();
            if (frame is null) throw new InvalidOperationException("không chụp được khung hình");
            frame.Save(Path.Combine(directory, name + ".png"));
            window.Close();
            Console.WriteLine($"OK    {name}.png ({frame.PixelSize.Width}×{frame.PixelSize.Height})");
            return 0;
        }
        catch (Exception e)
        {
            Console.WriteLine($"FAIL  {name}: {e}");
            return 1;
        }
    }
}
