namespace AutoElevateLauncher;

public enum StartupItemType
{
    PowerShellScript,
    Executable
}

public enum StartupItemStatus
{
    NeverRun,
    Running,
    Succeeded,
    Failed,
    Stopped,
    Unknown
}

public enum TaskSyncStatus
{
    NotCreated,
    Synchronized,
    Failed
}

public sealed class StartupItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public StartupItemType Type { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string TaskName { get; set; } = string.Empty;
    public TaskSyncStatus TaskSyncStatus { get; set; } = TaskSyncStatus.NotCreated;
    public string LastTaskError { get; set; } = string.Empty;
    public DateTimeOffset? LastRunStartedAt { get; set; }
    public DateTimeOffset? LastRunFinishedAt { get; set; }
    public int? LastExitCode { get; set; }
    public int? LastProcessId { get; set; }
    public StartupItemStatus LastStatus { get; set; } = StartupItemStatus.NeverRun;

    public void EnsureTaskName()
    {
        if (string.IsNullOrWhiteSpace(TaskName))
        {
            TaskName = $"AutoElevateLauncher-{Id}";
        }
    }
}
