using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;

namespace AutoPowerRunner.Services;

public sealed class StartupTaskService
{
    public const string DefaultTaskName = "AutoPowerRunner";

    private readonly string _taskName;
    private readonly string _executablePath;
    private readonly LogService? _log;

    public StartupTaskService(string executablePath, LogService? log = null, string taskName = DefaultTaskName)
    {
        _executablePath = executablePath;
        _log = log;
        _taskName = taskName;
    }

    public static string BuildCreateArguments(string taskName, string executablePath, string userName)
    {
        EnsureNoDoubleQuote(taskName, nameof(taskName));
        EnsureNoDoubleQuote(executablePath, nameof(executablePath));
        EnsureNoDoubleQuote(userName, nameof(userName));

        var quotedTarget = $"\\\"{executablePath}\\\"";
        return $"/Create /TN \"{taskName}\" /SC ONLOGON /RL HIGHEST /RU \"{userName}\" /TR \"{quotedTarget}\" /F";
    }

    public static string BuildDeleteArguments(string taskName)
    {
        EnsureNoDoubleQuote(taskName, nameof(taskName));

        return $"/Delete /TN \"{taskName}\" /F";
    }

    public static string BuildQueryArguments(string taskName)
    {
        EnsureNoDoubleQuote(taskName, nameof(taskName));

        return $"/Query /TN \"{taskName}\"";
    }

    public static string GetSchtasksPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "schtasks.exe");
    }

    public bool IsEnabled()
    {
        var result = RunSchtasks(BuildQueryArguments(_taskName), runAsAdmin: false, operation: "query", taskName: _taskName);
        return result.ExitCode == 0;
    }

    public void Enable()
    {
        var userName = WindowsIdentity.GetCurrent().Name;
        var args = BuildCreateArguments(_taskName, _executablePath, userName);
        var result = RunSchtasks(args, runAsAdmin: true, operation: "create", taskName: _taskName);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildExitFailureMessage("create", _taskName, result));
        }

        _log?.Info("Administrator autostart enabled.");
    }

    public void Disable()
    {
        var result = RunSchtasks(BuildDeleteArguments(_taskName), runAsAdmin: true, operation: "delete", taskName: _taskName);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildExitFailureMessage("delete", _taskName, result));
        }

        _log?.Info("Administrator autostart disabled.");
    }

    private static CommandResult RunSchtasks(string arguments, bool runAsAdmin, string operation, string taskName)
    {
        return RunSchtasksAsync(arguments, runAsAdmin, operation, taskName).GetAwaiter().GetResult();
    }

    private static async Task<CommandResult> RunSchtasksAsync(string arguments, bool runAsAdmin, string operation, string taskName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GetSchtasksPath(),
            Arguments = arguments,
            UseShellExecute = runAsAdmin,
            Verb = runAsAdmin ? "runas" : "",
            CreateNoWindow = !runAsAdmin,
            RedirectStandardOutput = !runAsAdmin,
            RedirectStandardError = !runAsAdmin
        };

        using var process = StartProcess(startInfo, operation, taskName);

        var outputTask = startInfo.RedirectStandardOutput ? process.StandardOutput.ReadToEndAsync() : Task.FromResult("");
        var errorTask = startInfo.RedirectStandardError ? process.StandardError.ReadToEndAsync() : Task.FromResult("");

        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;
        return new CommandResult(process.ExitCode, output, error);
    }

    private static Process StartProcess(ProcessStartInfo startInfo, string operation, string taskName)
    {
        try
        {
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException(BuildStartFailureMessage(operation, taskName));
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException(BuildStartFailureMessage(operation, taskName), exception);
        }
    }

    private static void EnsureNoDoubleQuote(string value, string parameterName)
    {
        if (value.Contains('"'))
        {
            throw new ArgumentException("Value cannot contain double quotes.", parameterName);
        }
    }

    private static string BuildStartFailureMessage(string operation, string taskName)
    {
        return $"Failed to {operation} startup task '{taskName}': administrator authorization is required or schtasks.exe could not be started.";
    }

    private static string BuildExitFailureMessage(string operation, string taskName, CommandResult result)
    {
        return $"Failed to {operation} startup task '{taskName}'. Exit code: {result.ExitCode}. Output: {result.Output} Error: {result.Error}";
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
