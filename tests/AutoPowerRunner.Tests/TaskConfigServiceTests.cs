using System.Text.Json;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Tests;

public sealed class TaskConfigServiceTests
{
    [Fact]
    public void ManagedTask_SerializesExpectedFields()
    {
        var task = new ManagedTask
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Daily script",
            Type = ManagedTaskType.PowerShellScript,
            Path = @"C:\Scripts\daily.ps1",
            Arguments = "-Verbose",
            WorkingDirectory = @"C:\Scripts",
            RunMode = ManagedTaskRunMode.RunOnce,
            IsEnabled = true,
            LastResult = new TaskRuntimeResult
            {
                Status = TaskRuntimeStatus.Exited,
                ExitCode = 0,
                StartedAt = new DateTimeOffset(2026, 7, 5, 8, 0, 0, TimeSpan.Zero),
                ExitedAt = new DateTimeOffset(2026, 7, 5, 8, 0, 5, TimeSpan.Zero),
                Error = null
            }
        };

        var json = JsonSerializer.Serialize(task);

        Assert.Contains("\"Name\":\"Daily script\"", json);
        Assert.Contains("\"Type\":0", json);
        Assert.Contains("\"RunMode\":0", json);
        Assert.Contains("\"IsEnabled\":true", json);
        Assert.Contains("\"ExitCode\":0", json);
    }
}
