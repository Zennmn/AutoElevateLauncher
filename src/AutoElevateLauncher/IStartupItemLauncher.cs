namespace AutoElevateLauncher;

public interface IStartupItemLauncher
{
    Task<int> RunAsync(StartupConfig config, StartupItem item, CancellationToken cancellationToken = default);
}
