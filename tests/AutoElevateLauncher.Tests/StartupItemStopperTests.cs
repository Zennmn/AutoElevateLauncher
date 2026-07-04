using System.Diagnostics;
using AutoElevateLauncher;

namespace AutoElevateLauncher.Tests;

public sealed class StartupItemStopperTests
{
    [Fact]
    public async Task StopAsync_KillsRecordedMainProcessAndMarksItemStopped()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -Command Start-Sleep -Seconds 60",
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Failed to start test process.");

        var directory = Path.Combine(Path.GetTempPath(), "AutoElevateLauncherTests", Guid.NewGuid().ToString("N"));
        var store = new ConfigStore(directory);
        var item = new StartupItem
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Sleep",
            Type = StartupItemType.Executable,
            LastStatus = StartupItemStatus.Running,
            LastProcessId = process.Id
        };
        var config = new StartupConfig { Items = [item] };
        var stopper = new StartupItemStopper(store);

        try
        {
            var stopped = await stopper.StopAsync(config, item);

            Assert.True(stopped);
            Assert.Equal(StartupItemStatus.Stopped, item.LastStatus);
            Assert.NotNull(item.LastRunFinishedAt);
            Assert.Empty(item.LastTaskError);
            Assert.False(IsProcessRunning(process.Id));
        }
        finally
        {
            KillIfRunning(process);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static bool IsProcessRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                process.WaitForExit(5000);
            }
        }
        catch
        {
        }
    }
}
