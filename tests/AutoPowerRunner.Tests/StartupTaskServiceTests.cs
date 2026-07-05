using AutoPowerRunner.Services;

namespace AutoPowerRunner.Tests;

public sealed class StartupTaskServiceTests
{
    [Fact]
    public void BuildCreateArguments_ReturnsExactExpectedCommand()
    {
        var args = StartupTaskService.BuildCreateArguments(
            taskName: "AutoPowerRunner",
            executablePath: @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            userName: @"DESKTOP\User");

        Assert.Equal(
            "/Create /TN \"AutoPowerRunner\" /SC ONLOGON /RL HIGHEST /RU \"DESKTOP\\User\" /TR \"\\\"C:\\Program Files\\AutoPowerRunner\\AutoPowerRunner.exe\\\"\" /F",
            args);
    }

    [Fact]
    public void BuildDeleteArguments_ForcesDeletion()
    {
        var args = StartupTaskService.BuildDeleteArguments("AutoPowerRunner");

        Assert.Equal("/Delete /TN \"AutoPowerRunner\" /F", args);
    }

    [Fact]
    public void BuildQueryArguments_BuildsQueryCommand()
    {
        var args = StartupTaskService.BuildQueryArguments("AutoPowerRunner");

        Assert.Equal("/Query /TN \"AutoPowerRunner\"", args);
    }

    [Fact]
    public void BuildCreateArguments_RejectsQuotesInTaskName()
    {
        var exception = Assert.Throws<ArgumentException>(() => StartupTaskService.BuildCreateArguments(
            taskName: "Auto\"PowerRunner",
            executablePath: @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            userName: @"DESKTOP\User"));

        Assert.Equal("taskName", exception.ParamName);
    }

    [Fact]
    public void BuildCreateArguments_RejectsQuotesInExecutablePath()
    {
        var exception = Assert.Throws<ArgumentException>(() => StartupTaskService.BuildCreateArguments(
            taskName: "AutoPowerRunner",
            executablePath: "C:\\Program Files\\AutoPowerRunner\\Auto\"PowerRunner.exe",
            userName: @"DESKTOP\User"));

        Assert.Equal("executablePath", exception.ParamName);
    }

    [Fact]
    public void BuildCreateArguments_RejectsQuotesInUserName()
    {
        var exception = Assert.Throws<ArgumentException>(() => StartupTaskService.BuildCreateArguments(
            taskName: "AutoPowerRunner",
            executablePath: @"C:\Program Files\AutoPowerRunner\AutoPowerRunner.exe",
            userName: "DESKTOP\\Us\"er"));

        Assert.Equal("userName", exception.ParamName);
    }

    [Fact]
    public void GetSchtasksPath_UsesWindowsSystem32()
    {
        var path = StartupTaskService.GetSchtasksPath();
        var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        Assert.EndsWith(Path.Combine("System32", "schtasks.exe"), path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(windowsFolder, path, StringComparison.OrdinalIgnoreCase);
    }
}
