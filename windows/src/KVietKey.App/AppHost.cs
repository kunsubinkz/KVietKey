using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using KVietKey.App.UI;
using KVietKey.Core;

namespace KVietKey.App;

/// <summary>Khởi chạy và điều phối các cửa sổ: khay hệ thống, panel, cài đặt, chào mừng.</summary>
internal sealed class AppHost : IDisposable
{
    private readonly AppServices _s;
    private readonly IClassicDesktopStyleApplicationLifetime? _lifetime;
    private SettingsWindow? _settings;
    private OnboardingWindow? _onboarding;
    private MenuPanelWindow? _panel;
    private TrayController? _tray;
    private DispatcherTimer? _fullscreenTimer;

    public AppHost(AppServices services, IClassicDesktopStyleApplicationLifetime? lifetime)
    {
        _s = services;
        _lifetime = lifetime;
    }

    public void Start(bool autostart, SingleInstance? single)
    {
        _s.OpenSettings = ShowSettings;
        _s.Quit = Quit;
        _tray = new TrayController(_s, ShowPanel);

        bool inputOk = _s.StartInput();

        if (!_s.Prefs.Current.OnboardingDone && !autostart)
        {
            ShowOnboarding(1, inputOk);
        }
        else if (!inputOk)
        {
            new InfoDialog(Loc.Tr("Không thể bắt phím", "Unable to capture keystrokes"),
                Loc.Tr("Windows không cho KVietKey cài bộ bắt phím. Hãy kiểm tra phần mềm diệt virus (thêm KVietKey vào danh sách tin cậy) rồi mở lại ứng dụng.",
                    "Windows did not let KVietKey install its keyboard hook. Check your antivirus (allow KVietKey) and relaunch the app.")).Show();
        }
        else if (!autostart)
        {
            WarnAboutConflicts();
        }

        _fullscreenTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _fullscreenTimer.Tick += (_, _) =>
        {
            if (_s.Prefs.Current.DisableInFullscreen) _s.Controller.RefreshFullscreen();
        };
        _fullscreenTimer.Start();

        single?.Listen(command => Dispatcher.UIThread.Post(() =>
        {
            if (command == "show") ShowSettings(SettingsTab.Basic);
        }));
    }

    public void ShowPanel()
    {
        _panel ??= new MenuPanelWindow(_s);
        _panel.Toggle();
    }

    public void ShowSettings(SettingsTab tab)
    {
        if (_settings is null)
        {
            _settings = new SettingsWindow(_s);
            _settings.Closed += (_, _) => _settings = null;
        }
        _settings.ShowTab(tab);
    }

    private void ShowOnboarding(int step, bool inputOk)
    {
        if (_onboarding is not null)
        {
            _onboarding.Activate();
            return;
        }
        _onboarding = new OnboardingWindow(_s, step) { InputRunning = inputOk };
        _onboarding.ShowStep(step);
        _onboarding.Closed += (_, _) => _onboarding = null;
        _onboarding.Show();
    }

    private void WarnAboutConflicts()
    {
        var found = _s.System.FindConflictingInputApps();
        if (found.Count == 0) return;
        new InfoDialog(Loc.Tr("Phát hiện bộ gõ khác đang chạy", "Another input method is running"),
            Loc.Tr($"{string.Join(", ", found)} có thể xung đột với KVietKey. Hãy thoát chúng trước khi gõ.",
                $"{string.Join(", ", found)} may conflict with KVietKey. Quit it before typing.")).Show();
    }

    public void Quit()
    {
        Dispose();
        if (_lifetime is not null) _lifetime.Shutdown();
        else Environment.Exit(0);
    }

    public void Dispose()
    {
        _fullscreenTimer?.Stop();
        _tray?.Dispose();
        _tray = null;
        _s.StopInput();
    }
}
