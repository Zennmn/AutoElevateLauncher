using System.Text.Json;
using AutoPowerRunner.Models;
using AutoPowerRunner.Services;

namespace AutoPowerRunner.Tests;

public sealed class ResolutionSettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"AutoPowerRunner.Tests.{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadAsync_WhenMissing_ReturnsDefaults()
    {
        var service = CreateService();

        var settings = await service.LoadAsync();

        Assert.Equal(new DisplayResolution(2560, 1440), settings.FirstResolution);
        Assert.Equal(new DisplayResolution(1920, 1440), settings.SecondResolution);
        Assert.Equal(HotkeyModifiers.Control | HotkeyModifiers.Alt, settings.Modifiers);
        Assert.True(settings.IsEnabled);
    }

    [Fact]
    public async Task SaveAndLoadAsync_PreservesCustomSettings()
    {
        var service = CreateService();
        var expected = new ResolutionSwitchSettings
        {
            IsEnabled = true,
            FirstWidth = 3440,
            FirstHeight = 1440,
            SecondWidth = 2560,
            SecondHeight = 1440,
            Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
            VirtualKey = 0x52
        };

        await service.SaveAsync(expected);
        var actual = await service.LoadAsync();

        Assert.Equal(expected.FirstResolution, actual.FirstResolution);
        Assert.Equal(expected.SecondResolution, actual.SecondResolution);
        Assert.Equal(expected.Modifiers, actual.Modifiers);
        Assert.Equal(expected.VirtualKey, actual.VirtualKey);
    }

    [Fact]
    public async Task LoadAsync_WhenCorrupt_BacksUpFileAndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        var settingsFile = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(settingsFile, "{broken");
        var service = new ResolutionSettingsService(_directory, settingsFile);

        var settings = await service.LoadAsync();

        Assert.Equal(new DisplayResolution(2560, 1440), settings.FirstResolution);
        Assert.False(File.Exists(settingsFile));
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt.*.json"));
    }

    [Fact]
    public async Task LoadAsync_NormalizesInvalidValues()
    {
        Directory.CreateDirectory(_directory);
        var settingsFile = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(settingsFile, JsonSerializer.Serialize(new
        {
            FirstWidth = 1,
            FirstHeight = 1,
            SecondWidth = 1920,
            SecondHeight = 1080,
            Modifiers = 128,
            VirtualKey = 999
        }));
        var service = new ResolutionSettingsService(_directory, settingsFile);

        var settings = await service.LoadAsync();

        Assert.Equal(new DisplayResolution(2560, 1440), settings.FirstResolution);
        Assert.Equal(new DisplayResolution(1920, 1080), settings.SecondResolution);
        Assert.Equal(HotkeyModifiers.None, settings.Modifiers);
        Assert.InRange(settings.VirtualKey, 1, 0xFF);
    }

    [Fact]
    public async Task SaveAsync_DoesNotPersistComputedResolutionObjects()
    {
        var service = CreateService();

        await service.SaveAsync(new ResolutionSwitchSettings());
        var json = await File.ReadAllTextAsync(Path.Combine(_directory, "settings.json"));

        Assert.DoesNotContain("FirstResolution", json);
        Assert.DoesNotContain("SecondResolution", json);
    }

    [Fact]
    public async Task LoadAsync_WhenBothResolutionsAreEqual_NormalizesSecondResolution()
    {
        Directory.CreateDirectory(_directory);
        var settingsFile = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(settingsFile, """
            {
              "FirstWidth": 1920,
              "FirstHeight": 1080,
              "SecondWidth": 1920,
              "SecondHeight": 1080
            }
            """);
        var service = new ResolutionSettingsService(_directory, settingsFile);

        var settings = await service.LoadAsync();

        Assert.NotEqual(settings.FirstResolution, settings.SecondResolution);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private ResolutionSettingsService CreateService() =>
        new(_directory, Path.Combine(_directory, "settings.json"));
}
