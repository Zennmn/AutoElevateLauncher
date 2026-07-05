using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class ProcessRunner
{
    private readonly LogService? _log;
    private readonly ConcurrentDictionary<Guid, Process> _runningProcesses = new();

    public ProcessRunner(LogService? log = null)
    {
        _log = log;
    }

    public IReadOnlyCollection<Guid> RunningTaskIds => _runningProcesses.Keys.ToArray();

    public static ProcessStartInfo BuildStartInfo(ManagedTask task)
    {
        var workingDirectory = ResolveWorkingDirectory(task);

        if (task.Type == ManagedTaskType.PowerShellScript)
        {
            return new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{task.Path}\" {task.Arguments}".TrimEnd(),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false
            };
        }

        return new ProcessStartInfo
        {
            FileName = task.Path,
            Arguments = task.Arguments,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = false
        };
    }

    public Process Start(ManagedTask task, Action<ManagedTask>? onUpdated = null)
    {
        if (!File.Exists(task.Path))
        {
            MarkFailed(task, $"File not found: {task.Path}", onUpdated);
            throw new FileNotFoundException("Task target file was not found.", task.Path);
        }

        var startInfo = BuildStartInfo(task);
        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.Exited += (_, _) =>
        {
            task.LastResult.Status = TaskRuntimeStatus.Exited;
            task.LastResult.ExitCode = SafeExitCode(process);
            task.LastResult.ExitedAt = DateTimeOffset.Now;
            _runningProcesses.TryRemove(task.Id, out _);
            _log?.Info($"Task exited: {task.Name}, code {task.LastResult.ExitCode}");
            onUpdated?.Invoke(task);
            process.Dispose();
        };

        try
        {
            process.Start();
            task.LastResult.Status = TaskRuntimeStatus.Running;
            task.LastResult.ExitCode = null;
            task.LastResult.Error = null;
            task.LastResult.StartedAt = DateTimeOffset.Now;
            task.LastResult.ExitedAt = null;
            _runningProcesses[task.Id] = process;
            _log?.Info($"Task started: {task.Name}");
            onUpdated?.Invoke(task);
            return process;
        }
        catch (Exception ex)
        {
            process.Dispose();
            MarkFailed(task, ex.Message, onUpdated);
            _log?.Error($"Task failed to start: {task.Name}", ex);
            throw;
        }
    }

    public void Stop(Guid taskId)
    {
        if (!_runningProcesses.TryRemove(taskId, out var process))
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(milliseconds: 3000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
        }
        finally
        {
            process.Dispose();
        }
    }

    public void StopAll()
    {
        foreach (var taskId in RunningTaskIds)
        {
            Stop(taskId);
        }
    }

    private static string ResolveWorkingDirectory(ManagedTask task)
    {
        if (!string.IsNullOrWhiteSpace(task.WorkingDirectory))
        {
            return task.WorkingDirectory;
        }

        return Path.GetDirectoryName(task.Path) ?? Environment.CurrentDirectory;
    }

    private static int? SafeExitCode(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch
        {
            return null;
        }
    }

    private static void MarkFailed(ManagedTask task, string error, Action<ManagedTask>? onUpdated)
    {
        task.LastResult.Status = TaskRuntimeStatus.FailedToStart;
        task.LastResult.Error = error;
        task.LastResult.ExitCode = null;
        task.LastResult.StartedAt = null;
        task.LastResult.ExitedAt = DateTimeOffset.Now;
        onUpdated?.Invoke(task);
    }
}
