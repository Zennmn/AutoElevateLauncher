using System.IO;
using System.Text.Json;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class TaskConfigService : ITaskConfigService
{
    private readonly string _configDirectory;
    private readonly string _configFile;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public TaskConfigService(string configDirectory)
    {
        _configDirectory = configDirectory;
        _configFile = Path.Combine(configDirectory, "config.json");
    }

    public TaskConfigService(AppPaths paths) : this(paths.ConfigDirectory)
    {
    }

    public async Task<List<ManagedTask>> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_configDirectory);

        if (!File.Exists(_configFile))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_configFile);
            var tasks = await JsonSerializer.DeserializeAsync<List<ManagedTask>>(stream, _jsonOptions, cancellationToken);
            return tasks ?? [];
        }
        catch (JsonException)
        {
            BackupCorruptConfig();
            return [];
        }
    }

    public async Task SaveAsync(IReadOnlyCollection<ManagedTask> tasks, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_configDirectory);
        var tempFile = $"{_configFile}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = File.Create(tempFile))
            {
                await JsonSerializer.SerializeAsync(stream, tasks, _jsonOptions, cancellationToken);
            }

            if (File.Exists(_configFile))
            {
                File.Replace(tempFile, _configFile, null);
            }
            else
            {
                File.Move(tempFile, _configFile);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private void BackupCorruptConfig()
    {
        var backupPath = Path.Combine(_configDirectory, $"config.corrupt.{DateTimeOffset.Now:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.json");
        File.Move(_configFile, backupPath);
    }
}
