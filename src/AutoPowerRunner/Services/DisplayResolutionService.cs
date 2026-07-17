using System.ComponentModel;
using System.Runtime.InteropServices;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class DisplayResolutionService : IDisplayResolutionService
{
    private const int EnumCurrentSettings = -1;
    private const int DispChangeSuccessful = 0;
    private const int DispChangeRestart = 1;
    private const int DispChangeFailed = -1;
    private const int DispChangeBadMode = -2;
    private const int DispChangeNotUpdated = -3;
    private const int DispChangeBadFlags = -4;
    private const int DispChangeBadParam = -5;
    private const int DispChangeBadDualView = -6;
    private const int CdsUpdateRegistry = 0x00000001;
    private const int CdsTest = 0x00000002;
    private const int DmPelsWidth = 0x00080000;
    private const int DmPelsHeight = 0x00100000;

    public DisplayResolution GetCurrent()
    {
        var mode = CreateDevMode();
        if (!EnumDisplaySettings(null, EnumCurrentSettings, ref mode))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取主显示器当前分辨率。");
        }

        return new DisplayResolution(mode.dmPelsWidth, mode.dmPelsHeight);
    }

    public bool IsSupported(DisplayResolution resolution)
    {
        for (var index = 0; ; index++)
        {
            var mode = CreateDevMode();
            if (!EnumDisplaySettings(null, index, ref mode))
            {
                return false;
            }

            if (mode.dmPelsWidth == resolution.Width && mode.dmPelsHeight == resolution.Height)
            {
                return true;
            }
        }
    }

    public void Apply(DisplayResolution resolution)
    {
        var mode = CreateDevMode();
        if (!EnumDisplaySettings(null, EnumCurrentSettings, ref mode))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法读取主显示器当前显示模式。");
        }

        mode.dmPelsWidth = resolution.Width;
        mode.dmPelsHeight = resolution.Height;
        mode.dmFields = DmPelsWidth | DmPelsHeight;

        var testResult = ChangeDisplaySettings(ref mode, CdsTest);
        if (testResult != DispChangeSuccessful)
        {
            throw new InvalidOperationException(GetChangeErrorMessage(testResult, resolution));
        }

        var result = ChangeDisplaySettings(ref mode, CdsUpdateRegistry);
        if (result != DispChangeSuccessful)
        {
            throw new InvalidOperationException(GetChangeErrorMessage(result, resolution));
        }
    }

    private static DevMode CreateDevMode() => new()
    {
        dmDeviceName = string.Empty,
        dmFormName = string.Empty,
        dmSize = (short)Marshal.SizeOf<DevMode>()
    };

    internal static string GetChangeErrorMessage(int result, DisplayResolution resolution) => result switch
    {
        DispChangeRestart => $"切换到 {resolution} 需要重新启动系统后生效。",
        DispChangeFailed => $"显卡驱动切换到 {resolution} 失败。",
        DispChangeBadMode => $"显卡驱动不支持 {resolution}。",
        DispChangeNotUpdated => $"无法把 {resolution} 写入显示设置。",
        DispChangeBadFlags => $"切换到 {resolution} 时传入了无效标志。",
        DispChangeBadParam => $"切换到 {resolution} 时传入了无效参数。",
        DispChangeBadDualView => $"当前为双显示模式，无法切换到 {resolution}。",
        _ => $"无法切换到 {resolution}（错误代码 {result}）。"
    };

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode devMode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettings(ref DevMode devMode, int flags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
