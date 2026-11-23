#pragma warning disable CA1416 // Chỉ kiểm tra phần thuần logic/kích thước, không gọi API Windows thật.

using System.Runtime.InteropServices;
using KVietKey.Core;
using KVietKey.Platform.Windows;

namespace KVietKey.Core.Tests;

/// <summary>Kiểm tra những gì kiểm tra được trên macOS của lớp Windows: bố cục struct P/Invoke và logic thuần.</summary>
public class WindowsLayerTests
{
    [Fact]
    public void InputStructureMatchesWin32Layout()
    {
        // SendInput từ chối nếu cbSize sai: 40 trên x64/ARM64 (4 + 4 đệm + 32 của union).
        Assert.Equal(nint.Size == 8 ? 40 : 28, Marshal.SizeOf<NativeMethods.INPUT>());
        Assert.Equal(nint.Size == 8 ? 24 : 16, Marshal.SizeOf<NativeMethods.KEYBDINPUT>());
        Assert.Equal(nint.Size == 8 ? 24 : 20, Marshal.SizeOf<NativeMethods.KBDLLHOOKSTRUCT>());
        Assert.Equal(nint.Size == 8 ? 48 : 28, Marshal.SizeOf<NativeMethods.MSG>());
        Assert.Equal(40, Marshal.SizeOf<NativeMethods.MONITORINFO>());
        Assert.Equal(nint.Size == 8 ? 72 : 48, Marshal.SizeOf<NativeMethods.GUITHREADINFO>());
    }

    [Fact]
    public void KeyboardInputFieldOffsets()
    {
        Assert.Equal(0, (int)Marshal.OffsetOf<NativeMethods.KEYBDINPUT>(nameof(NativeMethods.KEYBDINPUT.wVk)));
        Assert.Equal(2, (int)Marshal.OffsetOf<NativeMethods.KEYBDINPUT>(nameof(NativeMethods.KEYBDINPUT.wScan)));
        Assert.Equal(4, (int)Marshal.OffsetOf<NativeMethods.KEYBDINPUT>(nameof(NativeMethods.KEYBDINPUT.dwFlags)));
        Assert.Equal(nint.Size == 8 ? 16 : 12, (int)Marshal.OffsetOf<NativeMethods.KEYBDINPUT>(nameof(NativeMethods.KEYBDINPUT.dwExtraInfo)));
        Assert.Equal(nint.Size == 8 ? 8 : 4, (int)Marshal.OffsetOf<NativeMethods.INPUT>(nameof(NativeMethods.INPUT.u)));
    }

    [Fact]
    public void MagicSpellsKvk1()
    {
        Assert.Equal((nuint)0x4B564B31, InputSender.Magic);
    }

    [Fact]
    public void ModifierTrackerFollowsLeftAndRightKeysIndependently()
    {
        var t = new ModifierTracker();
        Assert.True(t.Update(VirtualKeys.LControl, true));
        Assert.True(t.Update(VirtualKeys.RShift, true));
        Assert.Equal(ModifierKeys.Ctrl | ModifierKeys.Shift, t.Current);

        t.Update(VirtualKeys.LShift, true);
        t.Update(VirtualKeys.RShift, false);
        Assert.Equal(ModifierKeys.Ctrl | ModifierKeys.Shift, t.Current); // vẫn còn Shift trái

        t.Update(VirtualKeys.LShift, false);
        t.Update(VirtualKeys.LControl, false);
        Assert.Equal(ModifierKeys.None, t.Current);
        Assert.False(t.Update('A', true));
    }

    [Theory]
    [InlineData("", 16)]
    [InlineData("abc", 16)]
    [InlineData("0123456789abcdefXYZ", 16)]
    [InlineData("😀😀😀😀😀😀😀😀😀😀", 3)]
    [InlineData("aạ😀b😀c", 2)]
    public void ChunksNeverSplitSurrogatePairsOrLoseText(string text, int max)
    {
        var chunks = InputSender.Chunks(text, max).ToList();
        Assert.Equal(text, string.Concat(chunks));
        foreach (var c in chunks)
        {
            Assert.True(c.Length <= max);
            Assert.False(char.IsHighSurrogate(c[^1]), $"cắt giữa surrogate: {c}");
        }
    }
}
