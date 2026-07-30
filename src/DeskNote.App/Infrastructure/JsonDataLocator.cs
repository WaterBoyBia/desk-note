using System.IO;
using System.Text.Json;

namespace DeskNote.App.Infrastructure;

public interface IDataLocator
{
    Task<string> GetOrCreateAsync(CancellationToken cancellationToken = default);
    Task SetAsync(string dataDirectory, CancellationToken cancellationToken = default);
}

public sealed class JsonDataLocator : IDataLocator
{
    private readonly string locatorFile;
    private readonly string defaultDataDirectory;

    public JsonDataLocator(string? locatorFile = null, string? defaultDataDirectory = null)
    {
        var appRootOverride = Environment.GetEnvironmentVariable("DESKNOTE_STATE_ROOT");
        var appRoot = string.IsNullOrWhiteSpace(appRootOverride)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "desk-note")
            : Path.GetFullPath(appRootOverride);
        this.locatorFile = locatorFile ?? Path.Combine(appRoot, "locator.json");
        this.defaultDataDirectory = defaultDataDirectory ?? Path.Combine(appRoot, "data");
    }

    public async Task<string> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(locatorFile))
        {
            await SetAsync(defaultDataDirectory, cancellationToken);
        }

        var json = await File.ReadAllTextAsync(locatorFile, cancellationToken);
        var model = JsonSerializer.Deserialize<LocatorModel>(json)
            ?? throw new InvalidDataException("数据定位文件为空。");
        return Normalize(model.DataDirectory);
    }

    public Task SetAsync(string dataDirectory, CancellationToken cancellationToken = default)
    {
        var model = new LocatorModel(Normalize(dataDirectory));
        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
        return AtomicFile.WriteAllTextAsync(locatorFile, json, cancellationToken);
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("数据目录不能为空。", nameof(path));
        }

        return Path.GetFullPath(path);
    }

    private sealed record LocatorModel(string DataDirectory);
}
