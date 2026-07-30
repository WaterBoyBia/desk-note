using DeskNote.App.Infrastructure;
using System.Reflection;
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

    [Fact]
    public void Constructor_UsesStateRootEnvironmentOverrideForDefaultPaths()
    {
        var previous = Environment.GetEnvironmentVariable("DESKNOTE_STATE_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("DESKNOTE_STATE_ROOT", root);

            var locator = new JsonDataLocator();

            Assert.Equal(
                Path.Combine(Path.GetFullPath(root), "locator.json"),
                ReadPrivatePath(locator, "locatorFile"));
            Assert.Equal(
                Path.Combine(Path.GetFullPath(root), "data"),
                ReadPrivatePath(locator, "defaultDataDirectory"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DESKNOTE_STATE_ROOT", previous);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private static string ReadPrivatePath(JsonDataLocator locator, string fieldName) =>
        (string)(typeof(JsonDataLocator)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(locator)
            ?? throw new InvalidOperationException($"Field {fieldName} was not found."));
}
