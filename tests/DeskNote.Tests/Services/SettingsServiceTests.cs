using DeskNote.App.Infrastructure;
using DeskNote.App.Models;
using DeskNote.App.Services;
using Xunit;

namespace DeskNote.Tests.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");

    [Fact]
    public async Task InitializeAndUpdateAsync_PersistsSettings()
    {
        var service = CreateService();
        await service.InitializeAsync();

        await service.UpdateAsync(settings => settings.Theme = ThemeMode.Dark);

        var reloaded = CreateService();
        await reloaded.InitializeAsync();
        Assert.Equal(ThemeMode.Dark, reloaded.Current.Theme);
    }

    [Fact]
    public async Task InitializeAsync_CreatesRequiredDirectories()
    {
        var service = CreateService();

        await service.InitializeAsync();

        Assert.True(Directory.Exists(service.Paths.BackupsDirectory));
        Assert.True(Directory.Exists(service.Paths.LogsDirectory));
    }

    private SettingsService CreateService() => new(
        new JsonDataLocator(Path.Combine(root, "locator.json"), Path.Combine(root, "data")),
        new JsonSettingsStore());

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
