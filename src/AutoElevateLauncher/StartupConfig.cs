namespace AutoElevateLauncher;

public sealed class StartupConfig
{
    public List<StartupItem> Items { get; set; } = [];
    public bool StartManagerAtLogin { get; set; } = true;
}