using DeskNote.App.Infrastructure;
using Xunit;

namespace DeskNote.Tests.Infrastructure;

public sealed class JsonDataLocatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");

    [Fact]
    public async Task GetOrCreateAsync_CreatesDefaultAbsolutePath()
    {
        var locator = new JsonDataLocator(
            Path.Combine(root, "locator.json"),
            Path.Combine(root, "default-data"));

        var result = await locator.GetOrCreateAsync();

        Assert.Equal(Path.GetFullPath(Path.Combine(root, "default-data")), result);
        Assert.True(File.Exists(Path.Combine(root, "locator.json")));
    }

    [Fact]
    public async Task SetAsync_ReplacesExistingLocation()
    {
        var locator = new JsonDataLocator(
            Path.Combine(root, "locator.json"),
            Path.Combine(root, "default-data"));

        await locator.SetAsync(Path.Combine(root, "new-data"));

        Assert.Equal(Path.GetFullPath(Path.Combine(root, "new-data")), await locator.GetOrCreateAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
