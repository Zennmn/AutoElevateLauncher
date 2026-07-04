using System.Diagnostics;

namespace AutoElevateLauncher;

public interface IProcessRunner
{
    Task<ProcessCommandResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, CancellationToken cancellationToken = default);
    Task<ProcessCommandResult> RunElevatedAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessCommandResult> RunAsync(string fileName, string arguments, string? workingDirectory = null, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start {fileName}.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessCommandResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    public async Task<ProcessCommandResult> RunElevatedAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ProcessCommandResult(-1, string.Empty, "无法启动管理员权限操作。");
            }

            await process.WaitForExitAsync(cancellationToken);
            return new ProcessCommandResult(process.ExitCode, string.Empty, string.Empty);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new ProcessCommandResult(1223, string.Empty, "用户取消了管理员权限请求。");
        }
    }
}