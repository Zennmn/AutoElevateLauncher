using AutoPowerRunner.Models;
using AutoPowerRunner.Services;

namespace AutoPowerRunner.Tests;

public sealed class ProcessRunnerTests
{
    [Fact]
    public void BuildStartInfo_ForPowerShellScript_UsesExecutionPolicyBypassAndFile()
    {
        var task = new ManagedTask
        {
            Type = ManagedTaskType.PowerShellScript,
            Path = @"C:\Scripts\boot.ps1",
            Arguments = "-Mode Fast",
            WorkingDirectory = @"C:\Scripts"
        };

        var startInfo = ProcessRunner.BuildStartInfo(task);

        Assert.Equal("powershell.exe", startInfo.FileName);
        Assert.Contains("-ExecutionPolicy Bypass", startInfo.Arguments);
        Assert.Contains("-File \"C:\\Scripts\\boot.ps1\"", startInfo.Arguments);
        Assert.EndsWith("-Mode Fast", startInfo.Arguments);
        Assert.Equal(@"C:\Scripts", startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
    }

    [Fact]
    public void BuildStartInfo_ForExe_UsesExecutablePathAndArguments()
    {
        var task = new ManagedTask
        {
            Type = ManagedTaskType.Executable,
            Path = @"C:\Tools\agent.exe",
            Arguments = "--quiet",
            WorkingDirectory = ""
        };

        var startInfo = ProcessRunner.BuildStartInfo(task);

        Assert.Equal(@"C:\Tools\agent.exe", startInfo.FileName);
        Assert.Equal("--quiet", startInfo.Arguments);
        Assert.Equal(@"C:\Tools", startInfo.WorkingDirectory);
        Assert.False(startInfo.UseShellExecute);
    }
}
