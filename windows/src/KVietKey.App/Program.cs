using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KVietKey.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var cli = Cli.Parse(args);

        if (cli.SelfTest) return SelfTest.Run();
        if (cli.RenderUiDirectory is { } dir) return RenderUi.Run(dir);

        if (cli.TraceFile is { } trace) EnableTrace(trace);

        SingleInstance? single = cli.AllowMultiple ? null : new SingleInstance();
        if (single is { IsPrimary: false })
        {
            single.SendToPrimary("show");
            single.Dispose();
            return 0;
        }

        AppRuntime.Autostart = cli.Autostart;
        AppRuntime.Single = single;
        AppRuntime.DataDirectory = cli.DataDirectory;
        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnExplicitShutdown);
        }
        finally
        {
            single?.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .With(new FontManagerOptions { DefaultFamilyName = UI.KV.FontUri })
        .LogToTrace();

    /// <summary>Ghi vết để chẩn đoán (chỉ khi chạy với tham số này): thời điểm + phím + việc bộ gõ làm. CÓ chứa ký tự đã gõ.</summary>
    private static void EnableTrace(string path)
    {
        var writer = new StreamWriter(path, append: false) { AutoFlush = true };
        var start = System.Diagnostics.Stopwatch.StartNew();
        KVietKey.Core.InputController.Trace = line => writer.WriteLine($"{start.Elapsed.TotalMilliseconds,9:F1} {line}");
    }
}

internal sealed record Cli
{
    public bool SelfTest { get; init; }
    public string? RenderUiDirectory { get; init; }
    public string? TraceFile { get; init; }
    public string? DataDirectory { get; init; }
    public bool Autostart { get; init; }
    public bool AllowMultiple { get; init; }

    public static Cli Parse(string[] args)
    {
        string? Value(string name)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
        return new Cli
        {
            SelfTest = args.Contains("--selftest"),
            RenderUiDirectory = Value("--render-ui"),
            TraceFile = Value("--trace-file"),
            DataDirectory = Value("--data-dir"),
            Autostart = args.Contains("--autostart"),
            AllowMultiple = args.Contains("--allow-multiple"),
        };
    }
}
