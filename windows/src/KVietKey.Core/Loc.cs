using System.Globalization;

namespace KVietKey.Core;

/// <summary>Chọn tiếng Việt hoặc tiếng Anh cho giao diện theo cài đặt (mặc định theo ngôn ngữ hệ thống).</summary>
public static class Loc
{
    private static volatile AppLanguage _language = AppLanguage.Auto;

    public static AppLanguage Language
    {
        get => _language;
        set => _language = value;
    }

    public static bool IsVietnamese => _language switch
    {
        AppLanguage.Vi => true,
        AppLanguage.En => false,
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase),
    };

    public static string Tr(string vi, string en) => IsVietnamese ? vi : en;
}
