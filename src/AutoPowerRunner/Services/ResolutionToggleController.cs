using System.Windows.Input;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class ResolutionToggleController
{
    private readonly IDisplayResolutionService _displayService;

    public ResolutionToggleController(IDisplayResolutionService displayService)
    {
        _displayService = displayService;
    }

    public ResolutionToggleResult Toggle(ResolutionSwitchSettings settings)
    {
        var current = _displayService.GetCurrent();
        var first = settings.FirstResolution;
        var second = settings.SecondResolution;
        var target = current == first ? second : first;
        _displayService.Apply(target);
        return new ResolutionToggleResult(current, target);
    }
}

public sealed record ResolutionToggleResult(DisplayResolution Previous, DisplayResolution Current)
{
    public string Message => $"分辨率已从 {Previous} 切换为 {Current}";
}

public static class HotkeyFormatter
{
    public static string Format(ResolutionSwitchSettings settings)
    {
        var parts = new List<string>();
        if (settings.Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (settings.Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (settings.Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (settings.Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");

        var key = KeyInterop.KeyFromVirtualKey(settings.VirtualKey);
        parts.Add(key == Key.None ? $"VK {settings.VirtualKey}" : key.ToString());
        return string.Join(" + ", parts);
    }
}
