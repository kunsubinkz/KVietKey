using System.Diagnostics;
using KVietKey.Core;
using Microsoft.Win32;
using static KVietKey.Platform.Windows.NativeMethods;

namespace KVietKey.Platform.Windows;

/// <summary>Khởi động cùng Windows (HKCU\...\Run), âm thanh chuyển Việt/Anh, phát hiện bộ gõ khác.</summary>
public sealed class WindowsSystemServices : ISystemServices
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "KVietKey";
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_ALIAS = 0x00010000;
    private const uint SND_NODEFAULT = 0x0002;

    private readonly string _exePath;

    public WindowsSystemServices(string? exePath = null)
    {
        _exePath = exePath ?? Environment.ProcessPath ?? "";
    }

    public bool IsLaunchAtLoginEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string value && value.Contains(_exePath, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetLaunchAtLogin(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(ValueName, $"\"{_exePath}\" --autostart");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Debug.WriteLine($"[Autostart] {e.Message}");
        }
    }

    public void PlayToggleSound(bool vietnameseOn) =>
        PlaySound(vietnameseOn ? "SystemAsterisk" : "SystemDefault", 0, SND_ALIAS | SND_ASYNC | SND_NODEFAULT);

    public IReadOnlyList<string> FindConflictingInputApps()
    {
        var found = new List<string>();
        foreach (var name in AppCompat.ConflictingInputApps)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                found.Add(p.ProcessName);
                p.Dispose();
            }
        }
        return found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
