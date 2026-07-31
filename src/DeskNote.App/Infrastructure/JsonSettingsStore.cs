using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeskNote.App.Models;

namespace DeskNote.App.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<AppSettings> LoadAsync(
        string dataDirectory,
        CancellationToken cancellationToken = default)
    {
        var path = new DataPaths(Path.GetFullPath(dataDirectory)).SettingsFile;
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (JsonException)
        {
            var quarantine = Path.Combine(
                Path.GetDirectoryName(path)!,
                $"settings.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");
            File.Move(path, quarantine);
            return new AppSettings();
        }
    }

    public Task SaveAsync(
        string dataDirectory,
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        var path = new DataPaths(Path.GetFullPath(dataDirectory)).SettingsFile;
        var json = JsonSerializer.Serialize(settings, Options);
        return AtomicFile.WriteAllTextAsync(path, json, cancellationToken);
    }
}
