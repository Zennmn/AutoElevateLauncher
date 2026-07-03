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
}