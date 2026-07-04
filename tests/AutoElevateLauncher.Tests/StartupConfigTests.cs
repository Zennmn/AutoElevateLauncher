using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class StartupConfigTests
{
    [Fact]
    public void NewConfig_DisablesAdministratorSelfStartByDefault()
    {
        var config = new StartupConfig();

        Assert.False(config.StartManagerAtLogin);
    }
}
