using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KVietKey.Platform.Windows;

/// <summary>
/// Truy vấn UI Automation (COM) để biết ô đang focus có phải ô mật khẩu không và có đang bôi chọn văn bản không.
/// Mọi lệnh gọi chạy trên một luồng riêng có giới hạn thời gian, vì ứng dụng đích có thể treo.
/// </summary>
public sealed class UiAutomation : IDisposable
{
    private const int UIA_IsPasswordPropertyId = 30019;
    private const int UIA_TextPatternId = 10014;

    private readonly BlockingCollection<Action> _queue = [];
    private readonly Thread _thread;
    private IUIAutomation? _automation;
    private bool _unavailable;

    public UiAutomation()
    {
        _thread = new Thread(Run) { Name = "KVietKey.UIA", IsBackground = true };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public void Dispose() => _queue.CompleteAdding();

    private void Run()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
        {
            try { work(); }
            catch (Exception e) { Debug.WriteLine($"[UIA] {e}"); }
        }
    }

    /// <summary>Chạy <paramref name="query"/> trên luồng UIA, chờ tối đa <paramref name="timeout"/>; hết giờ trả về <paramref name="fallback"/>.</summary>
    private T Query<T>(Func<IUIAutomation, T> query, TimeSpan timeout, T fallback)
    {
        if (_unavailable || _queue.IsAddingCompleted) return fallback;
        var done = new ManualResetEventSlim();
        T result = fallback;
        try
        {
            _queue.Add(() =>
            {
                try
                {
                    _automation ??= CreateAutomation();
                    if (_automation is null) return;
                    result = query(_automation);
                }
                catch (COMException) { /* phần tử biến mất hoặc ứng dụng không hỗ trợ */ }
                catch (InvalidCastException) { }
                finally { done.Set(); }
            });
        }
        catch (InvalidOperationException)
        {
            return fallback;
        }
        return done.Wait(timeout) ? result : fallback;
    }

    private IUIAutomation? CreateAutomation()
    {
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("ff48dba4-60ef-4201-aa87-54103eef594e"));
            return type is null ? null : (IUIAutomation?)Activator.CreateInstance(type);
        }
        catch (Exception e) when (e is COMException or InvalidCastException or NotSupportedException or PlatformNotSupportedException)
        {
            _unavailable = true;
            return null;
        }
    }

    /// <summary>Phần tử đang focus là ô mật khẩu. Hết thời gian (mặc định 150 ms) coi như không phải.</summary>
    public bool IsFocusedElementPassword(int timeoutMs = 150) => Query(automation =>
    {
        if (automation.GetFocusedElement(out var element) != 0 || element is null) return false;
        try
        {
            if (element.GetCurrentPropertyValue(UIA_IsPasswordPropertyId, out var value) != 0) return false;
            return value is true;
        }
        finally
        {
            Marshal.ReleaseComObject(element);
        }
    }, TimeSpan.FromMilliseconds(timeoutMs), false);

    /// <summary>Độ dài vùng đang bôi chọn trong ô nhập đang focus; null nếu không đọc được (hoặc quá hạn).</summary>
    public int? SelectionLength(int timeoutMs = 40) => Query<int?>(automation =>
    {
        if (automation.GetFocusedElement(out var element) != 0 || element is null) return null;
        try
        {
            if (element.GetCurrentPattern(UIA_TextPatternId, out var pattern) != 0 || pattern is not IUIAutomationTextPattern text)
                return null;
            try
            {
                if (text.GetSelection(out var ranges) != 0 || ranges is null) return null;
                try
                {
                    if (ranges.get_Length(out int count) != 0 || count == 0) return 0;
                    if (ranges.GetElement(0, out var range) != 0 || range is null) return null;
                    try
                    {
                        return range.GetText(-1, out var selected) == 0 ? selected.Length : null;
                    }
                    finally { Marshal.ReleaseComObject(range); }
                }
                finally { Marshal.ReleaseComObject(ranges); }
            }
            finally { Marshal.ReleaseComObject(text); }
        }
        finally
        {
            Marshal.ReleaseComObject(element);
        }
    }, TimeSpan.FromMilliseconds(timeoutMs), null);

    // Giao diện COM: chỉ khai báo đến hàm cần dùng; các hàm đứng trước phải có mặt để đúng vị trí trong vtable.
    // IID và thứ tự hàm lấy từ UIAutomationClient.h của Windows SDK.

    [ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomation
    {
        [PreserveSig] int CompareElements(nint el1, nint el2);
        [PreserveSig] int CompareRuntimeIds(nint a, nint b);
        [PreserveSig] int GetRootElement(out IUIAutomationElement root);
        [PreserveSig] int ElementFromHandle(nint hwnd, out IUIAutomationElement element);
        [PreserveSig] int ElementFromPoint(long pt, out IUIAutomationElement element);
        [PreserveSig] int GetFocusedElement(out IUIAutomationElement element);
    }

    [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationElement
    {
        [PreserveSig] int SetFocus();
        [PreserveSig] int GetRuntimeId(nint runtimeId);
        [PreserveSig] int FindFirst(int scope, nint condition, out IUIAutomationElement found);
        [PreserveSig] int FindAll(int scope, nint condition, nint found);
        [PreserveSig] int FindFirstBuildCache(int scope, nint condition, nint cache, out IUIAutomationElement found);
        [PreserveSig] int FindAllBuildCache(int scope, nint condition, nint cache, nint found);
        [PreserveSig] int BuildUpdatedCache(nint cache, out IUIAutomationElement updated);
        [PreserveSig] int GetCurrentPropertyValue(int propertyId, [MarshalAs(UnmanagedType.Struct)] out object value);
        [PreserveSig] int GetCurrentPropertyValueEx(int propertyId, int ignoreDefault, [MarshalAs(UnmanagedType.Struct)] out object value);
        [PreserveSig] int GetCachedPropertyValue(int propertyId, [MarshalAs(UnmanagedType.Struct)] out object value);
        [PreserveSig] int GetCachedPropertyValueEx(int propertyId, int ignoreDefault, [MarshalAs(UnmanagedType.Struct)] out object value);
        [PreserveSig] int GetCurrentPatternAs(int patternId, nint riid, nint pattern);
        [PreserveSig] int GetCachedPatternAs(int patternId, nint riid, nint pattern);
        [PreserveSig] int GetCurrentPattern(int patternId, [MarshalAs(UnmanagedType.IUnknown)] out object pattern);
    }

    [ComImport, Guid("32eba289-3583-42c9-9c59-3b6d9a1e9b6a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationTextPattern
    {
        [PreserveSig] int RangeFromPoint(long pt, out IUIAutomationTextRange range);
        [PreserveSig] int RangeFromChild(IUIAutomationElement child, out IUIAutomationTextRange range);
        [PreserveSig] int GetSelection(out IUIAutomationTextRangeArray ranges);
    }

    [ComImport, Guid("ce4ae76a-e717-4c98-81ea-47371d028eb6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationTextRangeArray
    {
        [PreserveSig] int get_Length(out int length);
        [PreserveSig] int GetElement(int index, out IUIAutomationTextRange element);
    }

    [ComImport, Guid("a543cc6a-f4ae-494b-8239-c814481187a8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationTextRange
    {
        [PreserveSig] int Clone(out IUIAutomationTextRange clone);
        [PreserveSig] int Compare(IUIAutomationTextRange range, out int areSame);
        [PreserveSig] int CompareEndpoints(int srcEndPoint, IUIAutomationTextRange range, int targetEndPoint, out int compValue);
        [PreserveSig] int ExpandToEnclosingUnit(int textUnit);
        [PreserveSig] int FindAttribute(int attr, [MarshalAs(UnmanagedType.Struct)] object val, int backward, out IUIAutomationTextRange found);
        [PreserveSig] int FindText([MarshalAs(UnmanagedType.BStr)] string text, int backward, int ignoreCase, out IUIAutomationTextRange found);
        [PreserveSig] int GetAttributeValue(int attr, [MarshalAs(UnmanagedType.Struct)] out object value);
        [PreserveSig] int GetBoundingRectangles(nint boundingRects);
        [PreserveSig] int GetEnclosingElement(out IUIAutomationElement enclosing);
        [PreserveSig] int GetText(int maxLength, [MarshalAs(UnmanagedType.BStr)] out string text);
    }
}
