namespace AutoElevateLauncher;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ValidationResult Success { get; } = new(true, []);
}

public static class StartupItemValidator
{
    public static ValidationResult Validate(StartupItem item)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            errors.Add("名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(item.Path))
        {
            errors.Add("路径不能为空。");
        }
        else if (!File.Exists(item.Path))
        {
            errors.Add($"文件不存在：{item.Path}");
        }
        else
        {
            var extension = System.IO.Path.GetExtension(item.Path);
            if (item.Type == StartupItemType.PowerShellScript && !extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("PowerShell 脚本项目必须使用 .ps1 文件。");
            }

            if (item.Type == StartupItemType.Executable && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("可执行程序项目必须使用 .exe 文件。");
            }
        }

        if (!string.IsNullOrWhiteSpace(item.WorkingDirectory) && !Directory.Exists(item.WorkingDirectory))
        {
            errors.Add($"工作目录不存在：{item.WorkingDirectory}");
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(false, errors);
    }
}