using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class ConfigStoreTests
{
    [Fact]
    public void Save_WritesConfigAndLeavesNoTempFileAfterSuccessfulSave()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AutoElevateLauncherTests", Guid.NewGuid().ToString("N"));
        var store = new ConfigStore(directory);
        var config = new StartupConfig
        {
            Items = [new StartupItem { Name = "测试", Path = "C:\\Tools\\demo.exe", Type = StartupItemType.Executable }]
        };

        try
        {
            store.Save(config);

            Assert.True(File.Exists(Path.Combine(directory, "config.json")));
            Assert.Empty(Directory.GetFiles(directory, "config.*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
