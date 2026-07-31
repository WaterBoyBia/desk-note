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

    [Fact]
    public async Task UpdateAsync_SerializesConcurrentWritesAndKeepsBothChanges()
    {
        var store = new BlockingSettingsStore(blockFirstSave: true);
        var service = CreateService(store);
        await service.InitializeAsync();

        var firstUpdate = service.UpdateAsync(settings => settings.Theme = ThemeMode.Dark);
        await store.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var secondUpdate = service.UpdateAsync(settings => settings.WindowOpacity = 0.55);

        Assert.Equal(1, store.MaximumConcurrentSaves);
        store.ReleaseFirstSave();
        await Task.WhenAll(firstUpdate, secondUpdate);

        Assert.Equal(1, store.MaximumConcurrentSaves);
        Assert.Equal(ThemeMode.Dark, service.Current.Theme);
        Assert.Equal(0.55, service.Current.WindowOpacity, precision: 2);
        Assert.Equal(ThemeMode.Dark, store.LastSaved.Theme);
        Assert.Equal(0.55, store.LastSaved.WindowOpacity, precision: 2);
    }

    [Fact]
    public async Task UpdateAsync_FailedWriteRestoresOnlyItsOwnSnapshot()
    {
        var store = new BlockingSettingsStore();
        var service = CreateService(store);
        await service.InitializeAsync();
        await service.UpdateAsync(settings => settings.Theme = ThemeMode.Dark);
        store.FailNextSave = true;

        await Assert.ThrowsAsync<IOException>(
            () => service.UpdateAsync(settings => settings.WindowOpacity = 0.45));

        Assert.Equal(ThemeMode.Dark, service.Current.Theme);
        Assert.Equal(AppSettings.DefaultWindowOpacity, service.Current.WindowOpacity, precision: 2);
    }

    private SettingsService CreateService(ISettingsStore? store = null) => new(
        new JsonDataLocator(Path.Combine(root, "locator.json"), Path.Combine(root, "data")),
        store ?? new JsonSettingsStore());

    private sealed class BlockingSettingsStore : ISettingsStore
    {
        private readonly bool blockFirstSave;
        private readonly TaskCompletionSource<bool> firstSaveRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int activeSaves;
        private int maximumConcurrentSaves;
        private int saveCount;

        public BlockingSettingsStore(bool blockFirstSave = false)
        {
            this.blockFirstSave = blockFirstSave;
        }

        public TaskCompletionSource<bool> FirstSaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FailNextSave { get; set; }
        public int MaximumConcurrentSaves => maximumConcurrentSaves;
        public AppSettings LastSaved { get; private set; } = new();

        public Task<AppSettings> LoadAsync(string dataDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(Clone(LastSaved));

        public async Task SaveAsync(
            string dataDirectory,
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            var concurrentSaves = Interlocked.Increment(ref activeSaves);
            UpdateMaximumConcurrentSaves(concurrentSaves);

            try
            {
                if (Interlocked.Increment(ref saveCount) == 1 && blockFirstSave)
                {
                    FirstSaveStarted.TrySetResult(true);
                    await firstSaveRelease.Task.WaitAsync(cancellationToken);
                }

                if (FailNextSave)
                {
                    FailNextSave = false;
                    throw new IOException("Save failed.");
                }

                LastSaved = Clone(settings);
            }
            finally
            {
                Interlocked.Decrement(ref activeSaves);
            }
        }

        public void ReleaseFirstSave() => firstSaveRelease.TrySetResult(true);

        private void UpdateMaximumConcurrentSaves(int concurrentSaves)
        {
            int observed;
            do
            {
                observed = maximumConcurrentSaves;
                if (concurrentSaves <= observed)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref maximumConcurrentSaves, concurrentSaves, observed) != observed);
        }

        private static AppSettings Clone(AppSettings source) => new()
        {
            SchemaVersion = source.SchemaVersion,
            Theme = source.Theme,
            StartWithWindows = source.StartWithWindows,
            AlwaysOnTop = source.AlwaysOnTop,
            WindowOpacity = source.WindowOpacity,
            IncompleteSortDirection = source.IncompleteSortDirection,
            WindowBounds = new WindowBounds
            {
                Left = source.WindowBounds.Left,
                Top = source.WindowBounds.Top,
                Width = source.WindowBounds.Width,
                Height = source.WindowBounds.Height,
                ScreenDeviceName = source.WindowBounds.ScreenDeviceName
            }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
