using AutoPowerRunner.Models;
using AutoPowerRunner.Services;

namespace AutoPowerRunner.Tests;

public sealed class ResolutionToggleControllerTests
{
    [Fact]
    public void Toggle_WhenCurrentIsFirst_AppliesSecond()
    {
        var display = new FakeDisplayResolutionService(new DisplayResolution(2560, 1440));
        var controller = new ResolutionToggleController(display);

        var result = controller.Toggle(new ResolutionSwitchSettings());

        Assert.Equal(new DisplayResolution(1920, 1440), display.Applied);
        Assert.Equal("分辨率已从 2560 × 1440 切换为 1920 × 1440", result.Message);
    }

    [Fact]
    public void Toggle_WhenCurrentIsSecond_AppliesFirst()
    {
        var display = new FakeDisplayResolutionService(new DisplayResolution(1920, 1440));
        var controller = new ResolutionToggleController(display);

        controller.Toggle(new ResolutionSwitchSettings());

        Assert.Equal(new DisplayResolution(2560, 1440), display.Applied);
    }

    [Fact]
    public void Toggle_WhenCurrentMatchesNeither_AppliesFirst()
    {
        var display = new FakeDisplayResolutionService(new DisplayResolution(1600, 900));
        var controller = new ResolutionToggleController(display);

        controller.Toggle(new ResolutionSwitchSettings());

        Assert.Equal(new DisplayResolution(2560, 1440), display.Applied);
    }

    private sealed class FakeDisplayResolutionService(DisplayResolution current) : IDisplayResolutionService
    {
        public DisplayResolution? Applied { get; private set; }
        public DisplayResolution GetCurrent() => current;
        public bool IsSupported(DisplayResolution resolution) => true;
        public void Apply(DisplayResolution resolution) => Applied = resolution;
    }
}
