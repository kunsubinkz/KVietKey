using System.Diagnostics;
using KVietKey.Core;
using static KVietKey.Platform.Windows.NativeMethods;

namespace KVietKey.Platform.Windows;

/// <summary>Gửi Backspace và chuỗi Unicode bằng SendInput. Mọi sự kiện mang dấu <see cref="Magic"/> để hook bỏ qua.</summary>
public sealed class InputSender : IInputSender
{
    /// <summary>Gắn vào <c>dwExtraInfo</c> của mọi sự kiện do KVietKey tạo ra ("KVK1").</summary>
    public const nuint Magic = 0x4B564B31;

    private const int MaxBatch = 128;

    private readonly UiAutomation? _uia;

    public InputSender(UiAutomation? uia = null)
    {
        _uia = uia;
    }

    public void Send(int backspaces, string text, SendOptions options)
    {
        if (backspaces > 0 && options.ClearAutocompleteSelection && (_uia?.SelectionLength() ?? 0) > 0)
        {
            // Gợi ý tự hoàn thành đang được bôi chọn: xóa nó trước để các Backspace sau đó xóa đúng chữ đã gõ.
            // Dùng Backspace thay vì Delete: nếu người dùng đang giữ Shift thì Shift+Delete là Cut / xóa mục khỏi lịch sử
            // trình duyệt, còn Shift+Backspace vẫn chỉ là Backspace.
            SendKey(VirtualKeys.Back, extended: false);
        }

        if (options.DelayMicros <= 0)
        {
            SendBatched(backspaces, text);
            return;
        }

        // Ứng dụng xử lý sự kiện chậm (Electron): gửi từng bước kèm độ trễ.
        for (int i = 0; i < backspaces; i++)
        {
            SendKey(VirtualKeys.Back, extended: false);
            Delay(options.DelayMicros);
        }
        if (backspaces > 0) Delay(options.DelayMicros);
        foreach (var chunk in Chunks(text, 16))
        {
            SendUnicode(chunk);
            Delay(options.DelayMicros);
        }
    }

    public void Repost(KeyInput key)
    {
        SendKey(key.VirtualKey, key.Extended, (ushort)key.ScanCode);
    }

    /// <summary>Gửi trọn Backspace + chuỗi trong ít lần SendInput nhất: các sự kiện trong cùng một lần gọi không bị xen vào.</summary>
    private static void SendBatched(int backspaces, string text)
    {
        var inputs = new List<INPUT>(Math.Min(backspaces * 2 + text.Length * 2, MaxBatch));
        void Flush()
        {
            if (inputs.Count == 0) return;
            Dispatch(inputs);
            inputs.Clear();
        }

        for (int i = 0; i < backspaces; i++)
        {
            inputs.Add(Key(VirtualKeys.Back, 0, 0));
            inputs.Add(Key(VirtualKeys.Back, 0, KEYEVENTF_KEYUP));
            if (inputs.Count >= MaxBatch) Flush();
        }
        foreach (var unit in text)
        {
            inputs.Add(Unicode(unit, 0));
            inputs.Add(Unicode(unit, KEYEVENTF_KEYUP));
            if (inputs.Count >= MaxBatch)
            {
                // Không cắt giữa hai nửa của cặp surrogate.
                if (char.IsHighSurrogate(unit)) continue;
                Flush();
            }
        }
        Flush();
    }

    private static void SendKey(int vk, bool extended, ushort scan = 0)
    {
        uint ext = extended ? KEYEVENTF_EXTENDEDKEY : 0;
        if (scan == 0) scan = (ushort)KeyTranslator.ScanCodeFor((uint)vk);
        Dispatch([Key(vk, scan, ext), Key(vk, scan, ext | KEYEVENTF_KEYUP)]);
    }

    private static void SendUnicode(string text)
    {
        var inputs = new List<INPUT>(text.Length * 2);
        foreach (var unit in text)
        {
            inputs.Add(Unicode(unit, 0));
            inputs.Add(Unicode(unit, KEYEVENTF_KEYUP));
        }
        Dispatch(inputs);
    }

    private static INPUT Key(int vk, ushort scan, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION { ki = new KEYBDINPUT { wVk = (ushort)vk, wScan = scan, dwFlags = flags, dwExtraInfo = Magic } },
    };

    private static INPUT Unicode(char unit, uint extraFlags) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0, wScan = unit, dwFlags = KEYEVENTF_UNICODE | extraFlags, dwExtraInfo = Magic } },
    };

    private static unsafe void Dispatch(IReadOnlyList<INPUT> inputs)
    {
        var array = new INPUT[inputs.Count];
        for (int i = 0; i < array.Length; i++) array[i] = inputs[i];
        fixed (INPUT* p = array)
        {
            uint sent = SendInput((uint)array.Length, p, sizeof(INPUT));
            if (sent != array.Length) Debug.WriteLine($"[InputSender] SendInput gửi {sent}/{array.Length}");
        }
    }

    /// <summary>Mỗi nhóm không quá <paramref name="maxUnits"/> đơn vị UTF-16, không cắt giữa cặp surrogate.</summary>
    internal static IEnumerable<string> Chunks(string text, int maxUnits)
    {
        int start = 0;
        while (start < text.Length)
        {
            int end = Math.Min(text.Length, start + maxUnits);
            if (end < text.Length && char.IsHighSurrogate(text[end - 1])) end--;
            yield return text[start..end];
            start = end;
        }
    }

    /// <summary>Chờ chính xác theo micro giây (Thread.Sleep quá thô: bước 1–15 ms).</summary>
    private static void Delay(int micros)
    {
        long ticks = micros * Stopwatch.Frequency / 1_000_000;
        long end = Stopwatch.GetTimestamp() + ticks;
        var spin = new SpinWait();
        while (Stopwatch.GetTimestamp() < end) spin.SpinOnce(sleep1Threshold: -1);
    }
}
