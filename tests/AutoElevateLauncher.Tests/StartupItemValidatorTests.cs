using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class StartupItemValidatorTests
{
    [Fact]
    public void Validate_AcceptsExistingPowerShellScript()
    {
        var script = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1");
        File.WriteAllText(script, "Write-Output 'ok'");

        var item = new StartupItem
        {
            Name = "Script",
            Type = StartupItemType.PowerShellScript,
            Path = script,
            WorkingDirectory = Path.GetDirectoryName(script)!
        };

        var result = StartupItemValidator.Validate(item);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_RejectsWrongExtensionForExecutable()
    {
        var script = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1");
        File.WriteAllText(script, "Write-Output 'ok'");

        var item = new StartupItem
        {
            Name = "Wrong Type",
            Type = StartupItemType.Executable,
            Path = script,
            WorkingDirectory = Path.GetDirectoryName(script)!
        };

        var result = StartupItemValidator.Validate(item);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains(".exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_RejectsMissingPath()
    {
        var item = new StartupItem
        {
            Name = "Missing",
            Type = StartupItemType.PowerShellScript,
            Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1")
        };

        var result = StartupItemValidator.Validate(item);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }
}