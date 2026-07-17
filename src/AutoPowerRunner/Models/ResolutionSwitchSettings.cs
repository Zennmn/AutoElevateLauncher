using System.Windows.Input;
using System.Text.Json.Serialization;

namespace AutoPowerRunner.Models;

public sealed record DisplayResolution(int Width, int Height)
{
    public override string ToString() => $"{Width} × {Height}";
}

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public sealed class ResolutionSwitchSettings
{
    public bool IsEnabled { get; set; } = true;
    public int FirstWidth { get; set; } = 2560;
    public int FirstHeight { get; set; } = 1440;
    public int SecondWidth { get; set; } = 1920;
    public int SecondHeight { get; set; } = 1440;
    public HotkeyModifiers Modifiers { get; set; } = HotkeyModifiers.Control | HotkeyModifiers.Alt;
    public int VirtualKey { get; set; } = KeyInterop.VirtualKeyFromKey(Key.F10);

    [JsonIgnore]
    public DisplayResolution FirstResolution => new(FirstWidth, FirstHeight);

    [JsonIgnore]
    public DisplayResolution SecondResolution => new(SecondWidth, SecondHeight);

    public ResolutionSwitchSettings Clone() => new()
    {
        IsEnabled = IsEnabled,
        FirstWidth = FirstWidth,
        FirstHeight = FirstHeight,
        SecondWidth = SecondWidth,
        SecondHeight = SecondHeight,
        Modifiers = Modifiers,
        VirtualKey = VirtualKey
    };

    public static ResolutionSwitchSettings Normalize(ResolutionSwitchSettings? settings)
    {
        var normalized = settings?.Clone() ?? new ResolutionSwitchSettings();
        if (!IsDimensionValid(normalized.FirstWidth, normalized.FirstHeight))
        {
            normalized.FirstWidth = 2560;
            normalized.FirstHeight = 1440;
        }

        if (!IsDimensionValid(normalized.SecondWidth, normalized.SecondHeight))
        {
            normalized.SecondWidth = 1920;
            normalized.SecondHeight = 1440;
        }

        if (normalized.FirstResolution == normalized.SecondResolution)
        {
            var replacement = normalized.FirstResolution == new DisplayResolution(1920, 1440)
                ? new DisplayResolution(2560, 1440)
                : new DisplayResolution(1920, 1440);
            normalized.SecondWidth = replacement.Width;
            normalized.SecondHeight = replacement.Height;
        }

        const HotkeyModifiers allModifiers = HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows;
        normalized.Modifiers &= allModifiers;
        if (normalized.VirtualKey is <= 0 or > 0xFF or 0x10 or 0x11 or 0x12 or 0x5B or 0x5C)
        {
            normalized.VirtualKey = KeyInterop.VirtualKeyFromKey(Key.F10);
        }

        return normalized;
    }

    public static bool IsDimensionValid(int width, int height) =>
        width is >= 640 and <= 16384 && height is >= 480 and <= 16384;
}
