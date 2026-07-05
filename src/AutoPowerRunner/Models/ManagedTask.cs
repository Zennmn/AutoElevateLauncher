namespace AutoPowerRunner.Models;

public sealed class ManagedTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public ManagedTaskType Type { get; set; } = ManagedTaskType.PowerShellScript;
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public ManagedTaskRunMode RunMode { get; set; } = ManagedTaskRunMode.RunOnce;
    public bool IsEnabled { get; set; } = true;
    public TaskRuntimeResult LastResult { get; set; } = new();

    public ManagedTask Clone()
    {
        return new ManagedTask
        {
            Id = Id,
            Name = Name,
            Type = Type,
            Path = Path,
            Arguments = Arguments,
            WorkingDirectory = WorkingDirectory,
            RunMode = RunMode,
            IsEnabled = IsEnabled,
            LastResult = new TaskRuntimeResult
            {
                Status = LastResult.Status,
                ExitCode = LastResult.ExitCode,
                StartedAt = LastResult.StartedAt,
                ExitedAt = LastResult.ExitedAt,
                Error = LastResult.Error
            }
        };
    }
}
