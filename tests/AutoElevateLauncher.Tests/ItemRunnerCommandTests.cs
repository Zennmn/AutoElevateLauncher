using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class ItemRunnerCommandTests
{
    [Fact]
    public void BuildPowerShellArguments_PreservesScriptPathAndUserArguments()
    {
        var item = new StartupItem
        {
            Type = StartupItemType.PowerShellScript,
            Path = "D:\\Scripts\\hello world.ps1",
            Arguments = "-Mode Auto -Config \"D:\\cfg\\a b.json\""
        };

        var arguments = ItemRunner.BuildPowerShellArguments(item);

        Assert.Contains("-ExecutionPolicy Bypass", arguments);
        Assert.Contains("-File \"D:\\Scripts\\hello world.ps1\"", arguments);
        Assert.Contains("-Mode Auto -Config \"D:\\cfg\\a b.json\"", arguments);
    }

    [Fact]
    public void BuildExecutableStartInfo_UsesExecutablePathArgumentsAndWorkingDirectory()
    {
        var item = new StartupItem
        {
            Type = StartupItemType.Executable,
            Path = "C:\\Tools\\app.exe",
            Arguments = "--silent --profile default",
            WorkingDirectory = "C:\\Tools"
        };

        var startInfo = ItemRunner.BuildExecutableStartInfo(item);

        Assert.Equal("C:\\Tools\\app.exe", startInfo.FileName);
        Assert.Equal("--silent --profile default", startInfo.Arguments);
        Assert.Equal("C:\\Tools", startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void ItemRunner_ImplementsStartupItemLauncher()
    {
        IStartupItemLauncher launcher = new ItemRunner(new ConfigStore());

        Assert.IsType<ItemRunner>(launcher);
    }

    [Fact]
    public async Task RunAsync_RecordsLastTaskErrorWhenExecutableLaunchFails()
    {
        var directory = Path.Combine(Path.GetTempPath(), "AutoElevateLauncherTests", Guid.NewGuid().ToString("N"));
        var store = new ConfigStore(directory);
        var runner = new ItemRunner(store);
        var item = new StartupItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "缺失程序",
            Type = StartupItemType.Executable,
            Path = Path.Combine(directory, "missing.exe"),
            WorkingDirectory = directory,
            LastTaskError = "旧错误"
        };
        var config = new StartupConfig { Items = [item] };

        try
        {
            Directory.CreateDirectory(directory);

            var exitCode = await runner.RunAsync(config, item);

            Assert.Equal(-1, exitCode);
            Assert.Equal(StartupItemStatus.Failed, item.LastStatus);
            Assert.False(string.IsNullOrWhiteSpace(item.LastTaskError));
            Assert.NotEqual("旧错误", item.LastTaskError);
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
