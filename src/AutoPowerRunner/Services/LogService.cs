using System.Collections.Concurrent;
using System.IO;

namespace AutoPowerRunner.Services;

public sealed class LogService
{
    private static readonly ConcurrentDictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _logFile;
    private readonly object _gate;

    public LogService(AppPaths paths)
    {
        _logFile = paths.LogFile;
        _gate = PathLocks.GetOrAdd(Path.GetFullPath(_logFile), static _ => new object());
    }

    public string LogFile => _logFile;

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        var detail = exception is null ? message : $"{message} {exception.GetType().Name}: {exception.Message}";
        Write("ERROR", detail);
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}{Environment.NewLine}";
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFile)!);
            File.AppendAllText(_logFile, line);
        }
    }
}
