using System.Diagnostics;

namespace AutoElevateLauncher;

public sealed class StartupItemStopper
{
    private readonly ConfigStore _configStore;

    public StartupItemStopper(ConfigStore configStore)
    {
        _configStore = configStore;
    }

    public async Task<bool> StopAsync(StartupConfig config, StartupItem item, CancellationToken cancellationToken = default)
    {
        if (item.LastProcessId is null)
        {
            item.LastTaskError = "没有可停止的进程记录。";
            _configStore.Save(config);
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(item.LastProcessId.Value);
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync(cancellationToken);
            }

            item.LastRunFinishedAt = DateTimeOffset.Now;
            item.LastExitCode = null;
            item.LastStatus = StartupItemStatus.Stopped;
            item.LastTaskError = string.Empty;
            _configStore.Save(config);
            return true;
        }
        catch (ArgumentException)
        {
            item.LastRunFinishedAt = DateTimeOffset.Now;
            item.LastStatus = StartupItemStatus.Stopped;
            item.LastTaskError = "进程已结束。";
            _configStore.Save(config);
            return false;
        }
        catch (Exception ex)
        {
            item.LastTaskError = ex.Message;
            _configStore.Save(config);
            return false;
        }
    }
}
