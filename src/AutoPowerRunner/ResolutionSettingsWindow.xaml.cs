using System.Windows;
using System.Windows.Input;
using AutoPowerRunner.Models;
using AutoPowerRunner.Services;
using MessageBox = System.Windows.MessageBox;

namespace AutoPowerRunner;

public partial class ResolutionSettingsWindow : Window
{
    private readonly IDisplayResolutionService _displayService;
    private HotkeyModifiers _modifiers;
    private int _virtualKey;

    public ResolutionSettingsWindow(ResolutionSwitchSettings settings, IDisplayResolutionService displayService)
    {
        InitializeComponent();
        _displayService = displayService;
        Result = settings.Clone();
        EnabledBox.IsChecked = settings.IsEnabled;
        FirstWidthBox.Text = settings.FirstWidth.ToString();
        FirstHeightBox.Text = settings.FirstHeight.ToString();
        SecondWidthBox.Text = settings.SecondWidth.ToString();
        SecondHeightBox.Text = settings.SecondHeight.ToString();
        _modifiers = settings.Modifiers;
        _virtualKey = settings.VirtualKey;
        UpdateHotkeyText();
    }

    public ResolutionSwitchSettings Result { get; private set; }

    private void HotkeyField_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        HotkeyField.Focus();
        e.Handled = true;
    }

    private void HotkeyField_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        HotkeyCaptureHint.Visibility = Visibility.Visible;
    }

    private void HotkeyField_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        HotkeyCaptureHint.Visibility = Visibility.Collapsed;
    }

    private void HotkeyField_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftAlt or Key.RightAlt or Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var capturedModifiers = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control)) capturedModifiers |= HotkeyModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Alt)) capturedModifiers |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Shift)) capturedModifiers |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) capturedModifiers |= HotkeyModifiers.Windows;

        if (capturedModifiers == HotkeyModifiers.None && key is < Key.F1 or > Key.F24)
        {
            MessageBox.Show(this, "普通按键至少需要搭配 Ctrl、Alt、Shift 或 Win；F1–F24 可单独使用。", "快捷键", MessageBoxButton.OK, MessageBoxImage.Information);
            e.Handled = true;
            return;
        }

        _modifiers = capturedModifiers;
        _virtualKey = KeyInterop.VirtualKeyFromKey(key);
        UpdateHotkeyText();
        e.Handled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadResolution(FirstWidthBox.Text, FirstHeightBox.Text, out var first) ||
            !TryReadResolution(SecondWidthBox.Text, SecondHeightBox.Text, out var second))
        {
            MessageBox.Show(this, "请填写有效分辨率：宽度 640–16384，高度 480–16384。", "分辨率", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (first == second)
        {
            MessageBox.Show(this, "两组分辨率不能完全相同。", "分辨率", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (EnabledBox.IsChecked == true && (!_displayService.IsSupported(first) || !_displayService.IsSupported(second)))
            {
                MessageBox.Show(this, "主显示器不支持其中一组分辨率，请检查宽高设置。", "分辨率", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"无法读取主显示器支持的分辨率。{Environment.NewLine}{ex.Message}", "分辨率", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        Result = new ResolutionSwitchSettings
        {
            IsEnabled = EnabledBox.IsChecked == true,
            FirstWidth = first.Width,
            FirstHeight = first.Height,
            SecondWidth = second.Width,
            SecondHeight = second.Height,
            Modifiers = _modifiers,
            VirtualKey = _virtualKey
        };
        DialogResult = true;
    }

    private static bool TryReadResolution(string widthText, string heightText, out DisplayResolution resolution)
    {
        var width = 0;
        var height = 0;
        var valid = int.TryParse(widthText, out width) &&
                    int.TryParse(heightText, out height) &&
                    ResolutionSwitchSettings.IsDimensionValid(width, height);
        resolution = new DisplayResolution(width, height);
        return valid;
    }

    private void UpdateHotkeyText()
    {
        var settings = new ResolutionSwitchSettings { Modifiers = _modifiers, VirtualKey = _virtualKey };
        HotkeyText.Text = HotkeyFormatter.Format(settings);
    }
}
