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
            errors.Add("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(item.Path))
        {
            errors.Add("Path is required.");
        }
        else if (!File.Exists(item.Path))
        {
            errors.Add($"Path does not exist: {item.Path}");
        }
        else
        {
            var extension = System.IO.Path.GetExtension(item.Path);
            if (item.Type == StartupItemType.PowerShellScript && !extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("PowerShell script items must use a .ps1 file.");
            }

            if (item.Type == StartupItemType.Executable && !extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Executable items must use a .exe file.");
            }
        }

        if (!string.IsNullOrWhiteSpace(item.WorkingDirectory) && !Directory.Exists(item.WorkingDirectory))
        {
            errors.Add($"Working directory does not exist: {item.WorkingDirectory}");
        }

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(false, errors);
    }
}