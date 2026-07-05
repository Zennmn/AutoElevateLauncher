using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AutoPowerRunner.Models;

public sealed class TaskRuntimeResult : INotifyPropertyChanged
{
    private TaskRuntimeStatus _status = TaskRuntimeStatus.NotRunning;
    private int? _exitCode;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _exitedAt;
    private string? _error;

    public TaskRuntimeStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public int? ExitCode
    {
        get => _exitCode;
        set
        {
            if (SetProperty(ref _exitCode, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public DateTimeOffset? StartedAt
    {
        get => _startedAt;
        set
        {
            if (SetProperty(ref _startedAt, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public DateTimeOffset? ExitedAt
    {
        get => _exitedAt;
        set => SetProperty(ref _exitedAt, value);
    }

    public string? Error
    {
        get => _error;
        set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(Summary));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    [JsonIgnore]
    public string Summary
    {
        get
        {
            return Status switch
            {
                TaskRuntimeStatus.NotRunning => "Not running",
                TaskRuntimeStatus.Running => StartedAt is null ? "Running" : $"Running since {StartedAt:yyyy-MM-dd HH:mm:ss}",
                TaskRuntimeStatus.Exited => ExitCode is null ? "Exited" : $"Exited with code {ExitCode}",
                TaskRuntimeStatus.FailedToStart => string.IsNullOrWhiteSpace(Error) ? "Failed to start" : $"Failed: {Error}",
                _ => Status.ToString()
            };
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
