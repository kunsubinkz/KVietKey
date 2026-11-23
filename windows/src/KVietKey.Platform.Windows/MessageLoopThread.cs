using System.Diagnostics;
using static KVietKey.Platform.Windows.NativeMethods;

namespace KVietKey.Platform.Windows;

/// <summary>Chạy một luồng riêng có vòng lặp thông điệp (cần cho hook mức thấp và WinEvent).</summary>
internal sealed class MessageLoopThread
{
    private readonly string _name;
    private readonly Action _onStart;
    private readonly Action _onStop;
    private readonly Action<MSG>? _onMessage;
    private readonly ManualResetEventSlim _ready = new();
    private Thread? _thread;
    private volatile uint _threadId;
    private Exception? _startError;

    public MessageLoopThread(string name, Action onStart, Action onStop, Action<MSG>? onMessage = null)
    {
        _name = name;
        _onStart = onStart;
        _onStop = onStop;
        _onMessage = onMessage;
    }

    public bool IsRunning => _thread is { IsAlive: true };

    /// <summary>Khởi động và chờ <c>onStart</c> chạy xong. Ném lại lỗi nếu <c>onStart</c> thất bại.</summary>
    public void Start()
    {
        if (IsRunning) return;
        _ready.Reset();
        _startError = null;
        _thread = new Thread(Run) { Name = _name, IsBackground = true, Priority = ThreadPriority.AboveNormal };
        _thread.Start();
        _ready.Wait();
        if (_startError is not null)
        {
            _thread.Join();
            _thread = null;
            throw _startError;
        }
    }

    public void Stop()
    {
        var thread = _thread;
        if (thread is null) return;
        if (_threadId != 0) PostThreadMessage(_threadId, WM_QUIT, 0, 0);
        thread.Join(TimeSpan.FromSeconds(3));
        _thread = null;
    }

    /// <summary>Đặt hẹn giờ gửi WM_TIMER vào vòng lặp (gọi từ luồng này, trong <c>onStart</c>).</summary>
    public static void SetThreadTimer(nuint id, uint intervalMs) => SetTimer(0, id, intervalMs, 0);

    private void Run()
    {
        try
        {
            _threadId = GetCurrentThreadId();
            _onStart();
        }
        catch (Exception e)
        {
            _startError = e;
            _ready.Set();
            return;
        }
        _ready.Set();

        try
        {
            while (GetMessage(out var msg, 0, 0, 0) > 0)
            {
                try { _onMessage?.Invoke(msg); }
                catch (Exception e) { Debug.WriteLine($"[{_name}] {e}"); }
                TranslateMessage(in msg);
                DispatchMessage(in msg);
            }
        }
        finally
        {
            try { _onStop(); }
            catch (Exception e) { Debug.WriteLine($"[{_name}] stop: {e}"); }
            _threadId = 0;
        }
    }
}
