namespace KVietKey.Engine;

/// <summary>Cấu hình của engine. Dùng <c>with</c> để tạo bản sao đã chỉnh sửa.</summary>
public sealed record EngineConfig
{
    public InputMethod Method { get; init; } = InputMethod.Telex;
    public Charset Charset { get; init; } = Charset.Unicode;

    /// <summary>Đặt dấu kiểu mới (hoà, thuý) thay vì kiểu cũ (hòa, thúy).</summary>
    public bool ModernTone { get; init; } = true;

    /// <summary>Cho phép bỏ dấu/áp dấu phụ ở bất kỳ vị trí nào trong từ (vd. <c>tieengs</c>, <c>tungw</c>).</summary>
    public bool FreeMarking { get; init; } = true;

    /// <summary>Không biến đổi khi phụ âm đầu không hợp lệ (vd. <c>class</c>, <c>show</c>).</summary>
    public bool SpellCheck { get; init; } = true;

    /// <summary>Khi ngắt từ, nếu từ không hợp lệ thì khôi phục đúng các phím đã gõ (vd. <c>windows</c>).</summary>
    public bool AutoRestore { get; init; } = true;

    public bool AutoCapitalize { get; init; }

    /// <summary>Cho phép z, f, w, j làm phụ âm đầu.</summary>
    public bool AllowZFWJ { get; init; }

    /// <summary>Phím <c>w</c> đứng một mình thành <c>ư</c> (Telex).</summary>
    public bool WAsU { get; init; } = true;

    public bool MacroEnabled { get; init; } = true;
    public bool MacroKeepCase { get; init; } = true;

    /// <summary>
    /// Đếm số Backspace theo code point thay vì theo cụm ký tự hiển thị.
    /// Windows thường chỉ xóa một code point mỗi lần Backspace, nên cần bật khi dùng Unicode tổ hợp
    /// (a + dấu thanh = 2 code point). macOS xóa cả cụm nên bản macOS luôn đếm theo cụm.
    /// </summary>
    public bool BackspacePerCodePoint { get; init; }
}
