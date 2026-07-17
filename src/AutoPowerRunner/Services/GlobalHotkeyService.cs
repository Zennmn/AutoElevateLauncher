using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0x4150;
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private IntPtr _windowHandle;
    private HwndSource? _source;
    private Action? _callback;
    private ResolutionSwitchSettings? _registeredSettings;

    public bool TryUpdate(IntPtr windowHandle, ResolutionSwitchSettings settings, Action callback, out string? error)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(callback);

        var previous = _registeredSettings?.Clone();
        var previousCallback = _callback;
        Unregister();

        if (!settings.IsEnabled)
        {
            error = null;
            return true;
        }

        if (TryRegister(windowHandle, settings, callback, out error))
        {
            return true;
        }

        if (previous is { IsEnabled: true } && previousCallback is not null)
        {
            _ = TryRegister(windowHandle, previous, previousCallback, out _);
        }

        return false;
    }

    public void Unregister()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            _ = UnregisterHotKey(_windowHandle, HotkeyId);
        }

        if (_source is not null)
        {
            _source.RemoveHook(WindowMessageHook);
        }

        _windowHandle = IntPtr.Zero;
        _source = null;
        _callback = null;
        _registeredSettings = null;
    }

    public void Dispose()
    {
        Unregister();
        GC.SuppressFinalize(this);
    }

    private bool TryRegister(IntPtr windowHandle, ResolutionSwitchSettings settings, Action callback, out string? error)
    {
        if (windowHandle == IntPtr.Zero)
        {
            error = "主窗口句柄尚未创建。";
            return false;
        }

        var source = HwndSource.FromHwnd(windowHandle);
        if (source is null)
        {
            error = "无法连接主窗口消息循环。";
            return false;
        }

        var nativeModifiers = (uint)settings.Modifiers | ModNoRepeat;
        if (!RegisterHotKey(windowHandle, HotkeyId, nativeModifiers, (uint)settings.VirtualKey))
        {
            var nativeError = new Win32Exception(Marshal.GetLastWin32Error());
            error = $"快捷键 {HotkeyFormatter.Format(settings)} 注册失败，可能已被其他程序占用。{nativeError.Message}";
            return false;
        }

        _windowHandle = windowHandle;
        _source = source;
        _callback = callback;
        _registeredSettings = settings.Clone();
        source.AddHook(WindowMessageHook);
        error = null;
        return true;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            _callback?.Invoke();
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
