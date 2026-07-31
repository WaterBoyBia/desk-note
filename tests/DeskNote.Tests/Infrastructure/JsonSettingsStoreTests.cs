using System.Globalization;
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
    public async Task LoadAsync_UsesFullOpacityWhenExistingJsonOmitsWindowOpacity()
    {
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, "settings.json"),
            "{\"schemaVersion\":1,\"theme\":\"light\"}");
        var store = new JsonSettingsStore();

        var settings = await store.LoadAsync(root);

        Assert.Equal(1.0, settings.WindowOpacity);
    }

    [Theory]
    [InlineData(-1.0, 0.20)]
    [InlineData(0.10, 0.20)]
    [InlineData(0.60, 0.60)]
    [InlineData(1.50, 1.00)]
    public async Task LoadAsync_ClampsWindowOpacity(double windowOpacity, double expected)
    {
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(
            Path.Combine(root, "settings.json"),
            $"{{\"schemaVersion\":1,\"theme\":\"light\",\"windowOpacity\":{windowOpacity.ToString(CultureInfo.InvariantCulture)}}}");
        var store = new JsonSettingsStore();

        var settings = await store.LoadAsync(root);

        Assert.Equal(expected, settings.WindowOpacity, precision: 2);
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
            WindowOpacity = 0.62,
            IncompleteSortDirection = TodoSortDirection.OldestFirst
        };

        await store.SaveAsync(root, expected);
        var actual = await store.LoadAsync(root);

        Assert.Equal(expected.Theme, actual.Theme);
        Assert.Equal(expected.AlwaysOnTop, actual.AlwaysOnTop);
        Assert.Equal(expected.StartWithWindows, actual.StartWithWindows);
        Assert.Equal(expected.WindowOpacity, actual.WindowOpacity);
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
