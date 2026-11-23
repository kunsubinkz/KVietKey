using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using KVietKey.Core;

namespace KVietKey.App;

/// <summary>Cấu hình do <see cref="Program"/> truyền vào trước khi Avalonia khởi động.</summary>
internal static class AppRuntime
{
    public static bool RenderOnly { get; set; }
    public static bool Autostart { get; set; }
    public static string? DataDirectory { get; set; }
    public static SingleInstance? Single { get; set; }
    public static AppHost? Host { get; private set; }

    public static void SetHost(AppHost host) => Host = host;
}

public partial class App : Application
{
    private AppServices? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (!AppRuntime.RenderOnly && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _services = AppServices.Create(AppRuntime.DataDirectory ?? Preferences.DefaultDirectory());
            var host = new AppHost(_services, desktop);
            AppRuntime.SetHost(host);
            desktop.Exit += (_, _) =>
            {
                host.Dispose();
                _services.Dispose();
            };
            host.Start(AppRuntime.Autostart, AppRuntime.Single);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
