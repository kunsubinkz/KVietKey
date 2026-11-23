using KVietKey.Engine;

namespace KVietKey.Core;

/// <summary>Lưu cài đặt (<c>settings.json</c>) và gõ tắt (<c>macros.json</c>) trong cùng một thư mục.</summary>
public sealed class Preferences
{
    public const string SettingsFileName = "settings.json";
    public const string MacrosFileName = "macros.json";

    private readonly object _gate = new();
    private readonly string _settingsPath;
    private readonly string _macrosPath;
    private volatile Settings _current;
    private volatile MacroTable _macros;

    public event Action<Settings, Settings>? SettingsChanged;
    public event Action? MacrosChanged;

    public string Directory { get; }

    /// <summary>Thư mục dữ liệu mặc định: cạnh exe nếu có <c>portable.txt</c>, nếu không là <c>%APPDATA%\KVietKey</c>.</summary>
    public static string DefaultDirectory(string? appDirectory = null)
    {
        appDirectory ??= AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(appDirectory, "portable.txt"))) return Path.Combine(appDirectory, "data");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KVietKey");
    }

    public Preferences(string directory)
    {
        Directory = directory;
        _settingsPath = Path.Combine(directory, SettingsFileName);
        _macrosPath = Path.Combine(directory, MacrosFileName);
        _current = File.Exists(_settingsPath) ? Settings.FromJson(SafeRead(_settingsPath)) : new Settings();
        _macros = MacroTable.Load(_macrosPath);
        Loc.Language = _current.Language;
    }

    private static string SafeRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch (IOException) { return "{}"; }
        catch (UnauthorizedAccessException) { return "{}"; }
    }

    /// <summary>Bản chụp cài đặt hiện tại: không bao giờ bị sửa sau khi công bố (an toàn khi đọc từ luồng hook).</summary>
    public Settings Current => _current;

    public MacroTable Macros => _macros;

    /// <summary>Sửa trên bản sao rồi công bố; chỉ lưu và báo thay đổi khi có khác biệt.</summary>
    public void Update(Action<Settings> mutate)
    {
        Settings old, updated;
        lock (_gate)
        {
            old = _current;
            updated = old.Clone();
            mutate(updated);
            if (updated.SameAs(old)) return;
            _current = updated;
            Loc.Language = updated.Language;
            Persist(updated);
        }
        SettingsChanged?.Invoke(old, updated);
    }

    public void SetMacros(MacroTable table)
    {
        lock (_gate)
        {
            if (_macros.Equals(table)) return;
            _macros = table.Clone();
            try { _macros.Save(_macrosPath); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* không lưu được: giữ trong bộ nhớ */ }
        }
        MacrosChanged?.Invoke();
    }

    private void Persist(Settings settings)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var temp = _settingsPath + ".tmp";
            File.WriteAllText(temp, settings.ToJson());
            File.Move(temp, _settingsPath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Thư mục chỉ đọc: vẫn chạy được với cài đặt trong bộ nhớ.
        }
    }
}
