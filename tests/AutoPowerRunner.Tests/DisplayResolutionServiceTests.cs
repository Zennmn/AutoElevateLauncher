using AutoPowerRunner.Models;
using AutoPowerRunner.Services;

namespace AutoPowerRunner.Tests;

public sealed class DisplayResolutionServiceTests
{
    private static readonly DisplayResolution Resolution = new(1920, 1440);

    [Theory]
    [InlineData(1, "切换到 1920 × 1440 需要重新启动系统后生效。")]
    [InlineData(-1, "显卡驱动切换到 1920 × 1440 失败。")]
    [InlineData(-2, "显卡驱动不支持 1920 × 1440。")]
    [InlineData(-3, "无法把 1920 × 1440 写入显示设置。")]
    [InlineData(-4, "切换到 1920 × 1440 时传入了无效标志。")]
    [InlineData(-5, "切换到 1920 × 1440 时传入了无效参数。")]
    [InlineData(-6, "当前为双显示模式，无法切换到 1920 × 1440。")]
    public void GetChangeErrorMessage_MapsWin32Result(int result, string expected)
    {
        Assert.Equal(expected, DisplayResolutionService.GetChangeErrorMessage(result, Resolution));
    }

    [Fact]
    public void GetChangeErrorMessage_PreservesUnknownResultCode()
    {
        Assert.Equal(
            "无法切换到 1920 × 1440（错误代码 99）。",
            DisplayResolutionService.GetChangeErrorMessage(99, Resolution));
    }
}
