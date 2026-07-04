using System.Text.Json;

namespace AutoElevateLauncher;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _appDataDirectory;
    private readonly string _configFile;
    private readonly string _logsDirectory;

    public ConfigStore() : this(AppPaths.AppDataDirectory)
    {
    }

    internal ConfigStore(string appDataDirectory)
    {
        _appDataDirectory = appDataDirectory;
        _configFile = Path.Combine(appDataDirectory, "config.json");
        _logsDirectory = Path.Combine(appDataDirectory, "logs");
    }

    public StartupConfig Load()
    {
        if (!File.Exists(_configFile))
        {
            return new StartupConfig();
        }

        string json;
        try
        {
            json = File.ReadAllText(_configFile);
        }
        catch (IOException)
        {
            return new StartupConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<StartupConfig>(json, JsonOptions) ?? new StartupConfig();
        }
        catch (JsonException)
        {
            BackUpCorruptedConfig();
            return new StartupConfig();
        }
    }

    private void BackUpCorruptedConfig()
    {
        try
        {
            var baseName = $"config.json.bad-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
            var path = Path.Combine(_appDataDirectory, baseName);
            var counter = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(_appDataDirectory, $"{baseName}-{counter}");
                counter++;
            }
            File.Move(_configFile, path);
        }
        catch
        {
        }
    }

    public void Save(StartupConfig config)
    {
        Directory.CreateDirectory(_appDataDirectory);
        Directory.CreateDirectory(_logsDirectory);

        foreach (var item in config.Items)
        {
            item.EnsureTaskName();
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        var tempPath = Path.Combine(_appDataDirectory, $"config.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tempPath, json);

        try
        {
            if (File.Exists(_configFile))
            {
                File.Replace(tempPath, _configFile, null);
            }
            else
            {
                File.Move(tempPath, _configFile);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
