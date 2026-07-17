using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class ResolutionSettingsService : IResolutionSettingsService
{
    private readonly string _configDirectory;
    private readonly string _settingsFile;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ResolutionSettingsService(AppPaths paths)
        : this(paths.ConfigDirectory, paths.SettingsFile)
    {
    }

    public ResolutionSettingsService(string configDirectory, string settingsFile)
    {
        _configDirectory = configDirectory;
        _settingsFile = settingsFile;
    }

    public async Task<ResolutionSwitchSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_configDirectory);
        if (!File.Exists(_settingsFile))
        {
            return new ResolutionSwitchSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_settingsFile);
            var settings = await JsonSerializer.DeserializeAsync<ResolutionSwitchSettings>(stream, _jsonOptions, cancellationToken);
            return ResolutionSwitchSettings.Normalize(settings);
        }
        catch (JsonException)
        {
            BackupCorruptSettings();
            return new ResolutionSwitchSettings();
        }

    }

    public async Task SaveAsync(ResolutionSwitchSettings settings, CancellationToken cancellationToken = default)
    {
        var snapshot = ResolutionSwitchSettings.Normalize(settings);
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_configDirectory);
            var tempFile = $"{_settingsFile}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var stream = File.Create(tempFile))
                {
                    await JsonSerializer.SerializeAsync(stream, snapshot, _jsonOptions, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                if (File.Exists(_settingsFile))
                {
                    File.Replace(tempFile, _settingsFile, null);
                }
                else
                {
                    File.Move(tempFile, _settingsFile);
                }
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private void BackupCorruptSettings()
    {
        var backupPath = Path.Combine(_configDirectory, $"settings.corrupt.{DateTimeOffset.Now:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.json");
        File.Move(_settingsFile, backupPath);
    }
}
