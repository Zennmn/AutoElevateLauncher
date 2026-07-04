using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class SelfStartSetupUiStateTests
{
    [Fact]
    public void FromConfig_ShowsSetupActionWhenAdministratorSelfStartIsNotConfigured()
    {
        var state = SelfStartSetupUiState.FromConfig(startManagerAtLogin: false);

        Assert.Equal("尚未配置管理员开机自启", state.StatusText);
        Assert.Equal("配置管理员自启", state.ButtonText);
        Assert.True(state.ButtonVisible);
        Assert.True(state.ButtonEnabled);
    }

    [Fact]
    public void FromConfig_ShowsConfiguredStateWithoutCloseActionWhenAdministratorSelfStartIsConfigured()
    {
        var state = SelfStartSetupUiState.FromConfig(startManagerAtLogin: true);

        Assert.Equal("管理员开机自启已配置", state.StatusText);
        Assert.Equal("配置管理员自启", state.ButtonText);
        Assert.False(state.ButtonVisible);
        Assert.False(state.ButtonEnabled);
    }
}
