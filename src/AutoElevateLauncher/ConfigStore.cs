using System.Text.Json;

namespace AutoElevateLauncher;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public StartupConfig Load()
    {
        if (!File.Exists(AppPaths.ConfigFile))
        {
            return new StartupConfig();
        }

        var json = File.ReadAllText(AppPaths.ConfigFile);
        return JsonSerializer.Deserialize<StartupConfig>(json, JsonOptions) ?? new StartupConfig();
    }

    public void Save(StartupConfig config)
    {
        Directory.CreateDirectory(AppPaths.AppDataDirectory);
        Directory.CreateDirectory(AppPaths.LogsDirectory);

        foreach (var item in config.Items)
        {
            item.EnsureTaskName();
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(AppPaths.ConfigFile, json);
    }
}