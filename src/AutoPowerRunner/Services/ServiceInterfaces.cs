using System.Diagnostics;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public interface ITaskConfigService
{
    Task<List<ManagedTask>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyCollection<ManagedTask> tasks, CancellationToken cancellationToken = default);
}

public interface IProcessRunner
{
    IReadOnlyCollection<Guid> RunningTaskIds { get; }
    Process Start(ManagedTask task, Action<ManagedTask>? onUpdated = null);
    void Stop(Guid taskId);
    void StopAll();
}

public interface IStartupTaskService
{
    bool IsEnabled();
    void Enable();
    void Disable();
}

public interface ILogService
{
    string LogFile { get; }
    void Info(string message);
    void Error(string message, Exception? exception = null);
}
