using DeskNote.App.Infrastructure;
using DeskNote.App.Models;
using Xunit;

namespace DeskNote.Tests.Infrastructure;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenFileDoesNotExist()
    {
        var store = new JsonSettingsStore();

        var settings = await store.LoadAsync(root);

        Assert.Equal(ThemeMode.Light, settings.Theme);
        Assert.Equal(TodoSortDirection.NewestFirst, settings.IncompleteSortDirection);
    }

    [Fact]
    public async Task SaveAsync_RoundTripsSettings()
    {
        var store = new JsonSettingsStore();
        var expected = new AppSettings
        {
            Theme = ThemeMode.Dark,
            AlwaysOnTop = true,
            StartWithWindows = true,
            IncompleteSortDirection = TodoSortDirection.OldestFirst
        };

        await store.SaveAsync(root, expected);
        var actual = await store.LoadAsync(root);

        Assert.Equal(expected.Theme, actual.Theme);
        Assert.Equal(expected.AlwaysOnTop, actual.AlwaysOnTop);
        Assert.Equal(expected.StartWithWindows, actual.StartWithWindows);
        Assert.Equal(expected.IncompleteSortDirection, actual.IncompleteSortDirection);
    }

    [Fact]
    public async Task LoadAsync_QuarantinesInvalidJson()
    {
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, "settings.json"), "not-json");
        var store = new JsonSettingsStore();

        var settings = await store.LoadAsync(root);

        Assert.Equal(ThemeMode.Light, settings.Theme);
        Assert.Single(Directory.GetFiles(root, "settings.corrupt-*.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
