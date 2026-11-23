using System.IO.Pipes;
using System.Text;

namespace KVietKey.App;

/// <summary>
/// Chỉ cho chạy một bản KVietKey cho mỗi người dùng: bản chạy sau gửi lệnh "show" cho bản đang chạy rồi thoát.
/// Dùng named mutex để xác định bản đầu tiên và named pipe để chuyển lệnh.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _cts = new();

    public bool IsPrimary { get; }

    public SingleInstance(string? name = null)
    {
        name ??= "KVietKey-" + Environment.UserName;
        _pipeName = name + ".pipe";
        _mutex = new Mutex(initiallyOwned: true, @"Local\" + name, out bool createdNew);
        IsPrimary = createdNew;
    }

    /// <summary>Gửi lệnh tới bản đang chạy (chỉ gọi khi <see cref="IsPrimary"/> là false). Trả về true nếu gửi được.</summary>
    public bool SendToPrimary(string command, int timeoutMs = 2000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(timeoutMs);
            var bytes = Encoding.UTF8.GetBytes(command + "\n");
            client.Write(bytes, 0, bytes.Length);
            client.Flush();
            return true;
        }
        catch (Exception e) when (e is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Bắt đầu nhận lệnh từ các bản chạy sau (gọi trên bản đầu tiên).</summary>
    public void Listen(Action<string> onCommand)
    {
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync(_cts.Token);
                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var line = await reader.ReadLineAsync(_cts.Token);
                    if (!string.IsNullOrWhiteSpace(line)) onCommand(line.Trim());
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                    await Task.Delay(200);
                }
            }
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        if (IsPrimary)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { /* đã được giải phóng */ }
        }
        _mutex.Dispose();
    }
}
