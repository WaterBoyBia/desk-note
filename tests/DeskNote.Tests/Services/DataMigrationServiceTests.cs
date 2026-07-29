using DeskNote.App.Infrastructure;
using DeskNote.App.Repositories;
using DeskNote.App.Services;
using Xunit;

namespace DeskNote.Tests.Services;

public sealed class DataMigrationServiceTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");
    private string current = string.Empty;
    private string target = string.Empty;

    public async Task InitializeAsync()
    {
        current = Path.Combine(root, "current");
        target = Path.Combine(root, "target");
        Directory.CreateDirectory(current);
        await DatabaseInitializer.InitializeAsync(Path.Combine(current, "todos.db"));
        await File.WriteAllTextAsync(Path.Combine(current, "settings.json"), "{\"schemaVersion\":1}");
    }

    [Fact]
    public async Task MigrateAsync_CopiesVerifiesAndSwitchesLocation()
    {
        var locator = new TestDataLocator(current);
        var settings = new SettingsService(locator, new JsonSettingsStore());
        await settings.InitializeAsync();
        var service = CreateService(settings, locator);

        var result = await service.MigrateAsync(target);

        Assert.True(result.Succeeded);
        Assert.Equal(Path.GetFullPath(target), settings.DataDirectory);
        Assert.True(File.Exists(Path.Combine(target, "todos.db")));
        Assert.True(File.Exists(Path.Combine(current, "todos.db")));
        Assert.True(await DatabaseInitializer.IsHealthyAsync(Path.Combine(target, "todos.db")));
    }

    [Fact]
    public async Task MigrateAsync_RollsBackWhenLocatorSwitchFails()
    {
        var locator = new TestDataLocator(current) { FailNextSet = true };
        var settings = new SettingsService(locator, new JsonSettingsStore());
        await settings.InitializeAsync();
        var service = CreateService(settings, locator);

        var result = await service.MigrateAsync(target);

        Assert.False(result.Succeeded);
        Assert.Equal(Path.GetFullPath(current), settings.DataDirectory);
        Assert.True(File.Exists(Path.Combine(current, "todos.db")));
    }

    [Fact]
    public async Task MigrateAsync_RollsBackWhenCancellationOccursAfterLocationSwitch()
    {
        using var cancellation = new CancellationTokenSource();
        var locator = new TestDataLocator(current) { HonorCancellation = true };
        var settings = new SettingsService(locator, new JsonSettingsStore());
        await settings.InitializeAsync();
        var service = new DataMigrationService(
            settings,
            locator,
            new DataOperationGate(),
            new FileSystemFacade(),
            new CancelOnSecondCheckVerifier(cancellation));

        var result = await service.MigrateAsync(target, cancellation.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(Path.GetFullPath(current), settings.DataDirectory);
        Assert.Equal(Path.GetFullPath(current), await locator.GetOrCreateAsync());
    }

    private static DataMigrationService CreateService(SettingsService settings, IDataLocator locator) => new(
        settings,
        locator,
        new DataOperationGate(),
        new FileSystemFacade(),
        new DatabaseVerifier());

    public Task DisposeAsync()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }

    private sealed class TestDataLocator(string initialPath) : IDataLocator
    {
        private string currentPath = Path.GetFullPath(initialPath);
        public bool FailNextSet { get; init; }
        public bool HonorCancellation { get; init; }

        public Task<string> GetOrCreateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(currentPath);

        public Task SetAsync(string dataDirectory, CancellationToken cancellationToken = default)
        {
            if (HonorCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (FailNextSet)
            {
                throw new IOException("locator write failed");
            }

            currentPath = Path.GetFullPath(dataDirectory);
            return Task.CompletedTask;
        }
    }

    private sealed class CancelOnSecondCheckVerifier(CancellationTokenSource cancellation) : IDatabaseVerifier
    {
        private int checkCount;

        public Task<bool> IsHealthyAsync(
            string databaseFile,
            CancellationToken cancellationToken = default)
        {
            checkCount++;
            if (checkCount == 2)
            {
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            return Task.FromResult(true);
        }
    }
}
