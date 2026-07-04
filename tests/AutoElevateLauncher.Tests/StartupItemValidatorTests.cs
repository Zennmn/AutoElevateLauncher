using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class StartupItemValidatorTests
{
    [Fact]
    public void Validate_AcceptsExistingPowerShellScript()
    {
        var script = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1");
        File.WriteAllText(script, "Write-Output 'ok'");

        try
        {
            var item = new StartupItem
            {
                Name = "脚本",
                Type = StartupItemType.PowerShellScript,
                Path = script,
                WorkingDirectory = Path.GetDirectoryName(script)!
            };

            var result = StartupItemValidator.Validate(item);

            Assert.True(result.IsValid);
            Assert.Empty(result.Errors);
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public void Validate_RejectsMissingNameWithChineseMessage()
    {
        var item = new StartupItem
        {
            Name = "",
            Type = StartupItemType.PowerShellScript,
            Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1")
        };

        var result = StartupItemValidator.Validate(item);

        Assert.False(result.IsValid);
        Assert.Contains("名称不能为空。", result.Errors);
    }

    [Fact]
    public void Validate_RejectsWrongExtensionForExecutableWithChineseMessage()
    {
        var script = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1");
        File.WriteAllText(script, "Write-Output 'ok'");

        try
        {
            var item = new StartupItem
            {
                Name = "类型错误",
                Type = StartupItemType.Executable,
                Path = script,
                WorkingDirectory = Path.GetDirectoryName(script)!
            };

            var result = StartupItemValidator.Validate(item);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, error => error.Contains("可执行程序项目必须使用 .exe 文件", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(script);
        }
    }

    [Fact]
    public void Validate_RejectsMissingPathWithChineseMessage()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".ps1");
        var item = new StartupItem
        {
            Name = "缺失文件",
            Type = StartupItemType.PowerShellScript,
            Path = missingPath
        };

        var result = StartupItemValidator.Validate(item);

        Assert.False(result.IsValid);
        Assert.Contains($"文件不存在：{missingPath}", result.Errors);
    }
}
