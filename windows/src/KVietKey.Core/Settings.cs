using System.Text.Json;
using System.Text.Json.Serialization;
using KVietKey.Engine;

namespace KVietKey.Core;

public enum AppMode { Auto, Vietnamese, English, Off }

public enum AppLanguage { Auto, Vi, En }

[Flags]
public enum ModifierKeys { None = 0, Ctrl = 1, Shift = 2, Alt = 4, Win = 8 }

/// <summary>Cấu hình riêng cho từng ứng dụng (khóa là tên file exe viết thường, vd. <c>chrome.exe</c>).</summary>
public sealed record AppRule
{
    public AppMode Mode { get; init; } = AppMode.Auto;
    public Charset? Charset { get; init; }
}

/// <summary>Phím tắt chuyển Việt ⇄ Anh: chỉ phím bổ trợ (KeyCode == null) hoặc tổ hợp bổ trợ + phím.</summary>
public sealed record Hotkey
{
    public const ModifierKeys All = ModifierKeys.Ctrl | ModifierKeys.Shift | ModifierKeys.Alt | ModifierKeys.Win;

    public int Modifiers { get; init; }
    public int? KeyCode { get; init; }

    public static readonly Hotkey CtrlShift = new() { Modifiers = (int)(ModifierKeys.Ctrl | ModifierKeys.Shift) };
    public static readonly Hotkey AltShift = new() { Modifiers = (int)(ModifierKeys.Alt | ModifierKeys.Shift) };
    public static readonly Hotkey CtrlAlt = new() { Modifiers = (int)(ModifierKeys.Ctrl | ModifierKeys.Alt) };
    public static readonly Hotkey CtrlSpace = new() { Modifiers = (int)ModifierKeys.Ctrl, KeyCode = VirtualKeys.Space };

    [JsonIgnore]
    public ModifierKeys Mods => (ModifierKeys)Modifiers;

    /// <summary>Hợp lệ khi chỉ chứa 4 bit bổ trợ của Windows (file cài đặt của macOS dùng bit khác).</summary>
    [JsonIgnore]
    public bool IsValid => (Modifiers & ~(int)All) == 0 && (Modifiers != 0);

    [JsonIgnore]
    public string DisplayString
    {
        get
        {
            var parts = new List<string>();
            if (Mods.HasFlag(ModifierKeys.Ctrl)) parts.Add("Ctrl");
            if (Mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Mods.HasFlag(ModifierKeys.Win)) parts.Add("Win");
            if (KeyCode is { } k) parts.Add(VirtualKeys.Name(k));
            return string.Join(" + ", parts);
        }
    }
}

/// <summary>
/// Toàn bộ cài đặt. File JSON dùng cùng tên khóa với bản macOS; khóa lạ bị bỏ qua và khóa thiếu/sai kiểu dùng mặc định.
/// Khác biệt theo nền tảng: <c>hotkey</c> (bit bổ trợ), <c>appRules</c>/<c>lastAppState</c> (khóa là tên exe thay vì bundle ID).
/// </summary>
public sealed class Settings
{
    public InputMethod Method { get; set; } = InputMethod.Telex;
    public Charset Charset { get; set; } = Charset.Unicode;
    public bool ModernTone { get; set; } = true;
    public bool FreeMarking { get; set; } = true;
    public bool SpellCheck { get; set; } = true;
    public bool AutoRestore { get; set; } = true;
    public bool AutoCapitalize { get; set; }
    public bool AllowZFWJ { get; set; }
    public bool WAsU { get; set; } = true;
    public bool MacroEnabled { get; set; } = true;
    public bool MacroKeepCase { get; set; } = true;

    public bool IsVietnamese { get; set; } = true;
    public Hotkey Hotkey { get; set; } = Hotkey.CtrlShift;
    public bool LaunchAtLogin { get; set; }
    public bool SoundOnToggle { get; set; }
    public bool RememberAppState { get; set; } = true;
    public Dictionary<string, AppRule> AppRules { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> LastAppState { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool DisableInFullscreen { get; set; }
    public bool BrowserAutocompleteFix { get; set; } = true;
    public int ElectronDelayMicros { get; set; } = 1500;
    public AppLanguage Language { get; set; } = AppLanguage.Auto;
    public bool DebugLog { get; set; }
    public bool OnboardingDone { get; set; }
    public bool ShowTrayIcon { get; set; } = true;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = null,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public EngineConfig ToEngineConfig(Charset? charsetOverride = null)
    {
        var charset = charsetOverride ?? Charset;
        return new EngineConfig
        {
            Method = Method,
            Charset = charset,
            ModernTone = ModernTone,
            FreeMarking = FreeMarking,
            SpellCheck = SpellCheck,
            AutoRestore = AutoRestore,
            AutoCapitalize = AutoCapitalize,
            AllowZFWJ = AllowZFWJ,
            WAsU = WAsU,
            MacroEnabled = MacroEnabled,
            MacroKeepCase = MacroKeepCase,
            // Windows chỉ xóa một code point mỗi lần Backspace (a + dấu tổ hợp = 2 lần).
            BackspacePerCodePoint = charset == Charset.UnicodeComposite,
        };
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Đọc từng khóa riêng lẻ: khóa lỗi chỉ ảnh hưởng chính nó.</summary>
    public static Settings FromJson(string json)
    {
        var settings = new Settings();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return settings;
            var props = typeof(Settings).GetProperties()
                .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                .ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                if (!props.TryGetValue(entry.Name, out var prop)) continue;
                try
                {
                    var value = entry.Value.Deserialize(prop.PropertyType, JsonOptions);
                    if (value is null) continue;
                    if (value is Hotkey { IsValid: false }) continue;
                    if (value is Dictionary<string, AppRule> rules) value = new Dictionary<string, AppRule>(rules.Where(r => r.Value is not null), StringComparer.OrdinalIgnoreCase);
                    else if (value is Dictionary<string, bool> states) value = new Dictionary<string, bool>(states, StringComparer.OrdinalIgnoreCase);
                    else if (value is Enum e && !Enum.IsDefined(e.GetType(), e)) continue;
                    prop.SetValue(settings, value);
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException or ArgumentException)
                {
                    // Giữ giá trị mặc định cho khóa này.
                }
            }
        }
        catch (JsonException)
        {
            // File hỏng: dùng toàn bộ mặc định.
        }
        return settings;
    }

    public Settings Clone() => FromJson(ToJson());

    public bool SameAs(Settings other) => ToJson() == other.ToJson();
}
