using System.Text;

namespace KVietKey.Core;

/// <summary>Một sự kiện nhấn phím (key down) do lớp nền tảng chuyển lên.</summary>
public readonly record struct KeyInput(
    int VirtualKey,
    ModifierKeys Modifiers,
    /// <summary>Ký tự thực sự được tạo ra bởi phím (đã tính bố cục bàn phím, Shift, CapsLock); null nếu không in được.</summary>
    Rune? Character,
    int ScanCode = 0,
    bool Extended = false);

public enum KeyDecision
{
    /// <summary>Cho phím đi tiếp tới ứng dụng.</summary>
    Pass,
    /// <summary>Nuốt phím: bộ gõ đã tự gửi thay thế.</summary>
    Swallow,
}

/// <summary>Tùy chọn gửi phím theo ứng dụng đang dùng.</summary>
public sealed record SendOptions
{
    /// <summary>Độ trễ (µs) giữa các bước gửi, cho ứng dụng xử lý sự kiện chậm (Electron).</summary>
    public int DelayMicros { get; init; }

    /// <summary>Xóa phần gợi ý tự hoàn thành (đang được bôi chọn) trước khi gửi Backspace.</summary>
    public bool ClearAutocompleteSelection { get; init; }

    public static readonly SendOptions Default = new();
}

/// <summary>Gửi phím tổng hợp (Backspace + chuỗi Unicode) vào luồng sự kiện.</summary>
public interface IInputSender
{
    void Send(int backspaces, string text, SendOptions options);

    /// <summary>Phát lại phím gốc (sau phần thay thế) với đúng mã phím và phím bổ trợ.</summary>
    void Repost(KeyInput key);
}

/// <summary>Theo dõi ứng dụng đang hoạt động phía trước.</summary>
public interface IForegroundMonitor
{
    /// <summary>Tên file exe viết thường của ứng dụng phía trước (vd. <c>chrome.exe</c>); null nếu chưa rõ.</summary>
    string? ProcessName { get; }

    /// <summary>Đang focus ô nhập mật khẩu (tương đương Secure Input của macOS). Phải rẻ, không chặn.</summary>
    bool IsSecureInput { get; }

    /// <summary>Ứng dụng phía trước chạy quyền admin: Windows không cho bộ gõ không-admin gửi phím vào đó (UIPI).</summary>
    bool IsForegroundElevated { get; }

    /// <summary>Cửa sổ phía trước phủ kín màn hình (trình chiếu, trò chơi, video toàn màn hình).</summary>
    bool IsFullscreen();

    event Action? Changed;
}

/// <summary>Các dịch vụ hệ điều hành phụ trợ.</summary>
public interface ISystemServices
{
    bool IsLaunchAtLoginEnabled { get; }
    void SetLaunchAtLogin(bool enabled);
    void PlayToggleSound(bool vietnameseOn);

    /// <summary>Tên các process bộ gõ khác đang chạy có thể xung đột (UniKey, EVKey…).</summary>
    IReadOnlyList<string> FindConflictingInputApps();
}
