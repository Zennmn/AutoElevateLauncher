namespace AutoElevateLauncher;

internal sealed record SelfStartSetupUiState(string StatusText, string ButtonText, bool ButtonVisible, bool ButtonEnabled)
{
    public static SelfStartSetupUiState FromConfig(bool startManagerAtLogin)
    {
        return startManagerAtLogin
            ? new SelfStartSetupUiState("管理员开机自启已配置", "配置管理员自启", false, false)
            : new SelfStartSetupUiState("尚未配置管理员开机自启", "配置管理员自启", true, true);
    }
}
