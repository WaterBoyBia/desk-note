using DeskNote.App.Infrastructure;
using DeskNote.App.Models;
using DeskNote.App.Services;
using DeskNote.App.ViewModels;
using Xunit;

namespace DeskNote.Tests.ViewModels;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");

    [Fact]
    public async Task SetStartWithWindowsCommand_RestoresOldValueWhenRegistryWriteFails()
    {
        var settings = new SettingsService(
            new JsonDataLocator(Path.Combine(root, "locator.json"), Path.Combine(root, "data")),
            new JsonSettingsStore());
        await settings.InitializeAsync();
        var viewModel = new SettingsViewModel(
            settings,
            new ThemeService(),
            new FailingStartupService(),
            new NoOpMigrationService());

        await viewModel.SetStartWithWindowsCommand.ExecuteAsync(true);

        Assert.False(viewModel.StartWithWindows);
        Assert.Equal("无法修改开机自启动。", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task WindowOpacity_DebouncesRapidChangesAndPersistsLatestValue()
    {
        var store = new RecordingSettingsStore();
        var settings = await CreateSettingsAsync(store);
        var viewModel = CreateViewModel(settings, TimeSpan.FromMilliseconds(20));

        viewModel.WindowOpacity = 0.80;
        viewModel.WindowOpacity = 0.60;
        viewModel.WindowOpacity = 0.35;
        await viewModel.PendingWindowOpacitySave.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Single(store.SavedOpacityValues);
        Assert.Equal(0.35, store.SavedOpacityValues[0]);
        Assert.Equal("35%", viewModel.WindowOpacityPercent);
        Assert.True(viewModel.IsLowOpacity);
    }

    [Fact]
    public async Task WindowOpacity_LatestFailureRestoresLastPersistedValue()
    {
        var store = new RecordingSettingsStore();
        var settings = await CreateSettingsAsync(store);
        var viewModel = CreateViewModel(settings, TimeSpan.FromMilliseconds(10));
        viewModel.WindowOpacity = 0.65;
        await viewModel.PendingWindowOpacitySave.WaitAsync(TimeSpan.FromSeconds(2));
        store.FailNextSave = true;

        viewModel.WindowOpacity = 0.30;
        await viewModel.PendingWindowOpacitySave.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0.65, viewModel.WindowOpacity);
        Assert.Equal("无法保存窗口透明度。", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task WindowOpacity_OldSuccessfulSaveBecomesRollbackBaselineWithoutReplacingPreview()
    {
        var store = new RecordingSettingsStore { BlockFirstSave = true };
        var settings = await CreateSettingsAsync(store);
        var viewModel = CreateViewModel(settings, TimeSpan.FromMilliseconds(10));
        viewModel.WindowOpacity = 0.70;
        await store.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        viewModel.WindowOpacity = 0.30;
        store.FailSecondSave = true;
        store.ReleaseFirstSave.TrySetResult(true);
        await viewModel.PendingWindowOpacitySave.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0.70, viewModel.WindowOpacity);
        Assert.Equal("无法保存窗口透明度。", viewModel.ErrorMessage);
    }

    private async Task<SettingsService> CreateSettingsAsync(ISettingsStore store)
    {
        var settings = new SettingsService(
            new JsonDataLocator(Path.Combine(root, "locator.json"), Path.Combine(root, "data")),
            store);
        await settings.InitializeAsync();
        return settings;
    }

    private static SettingsViewModel CreateViewModel(
        SettingsService settings,
        TimeSpan saveDelay) => new(
        settings,
        new ThemeService(),
        new NoOpStartupService(),
        new NoOpMigrationService(),
        saveDelay);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class FailingStartupService : IStartupService
    {
        public bool IsEnabled() => false;
        public void SetEnabled(bool enabled) => throw new UnauthorizedAccessException();
    }

    private sealed class NoOpStartupService : IStartupService
    {
        public bool IsEnabled() => false;

        public void SetEnabled(bool enabled)
        {
        }
    }

    private sealed class NoOpMigrationService : IDataMigrationService
    {
        public Task<DataMigrationResult> MigrateAsync(
            string targetDirectory,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DataMigrationResult.Success());
    }

    private sealed class RecordingSettingsStore : ISettingsStore
    {
        private int saveCount;

        public bool BlockFirstSave { get; set; }
        public bool FailNextSave { get; set; }
        public bool FailSecondSave { get; set; }
        public List<double> SavedOpacityValues { get; } = [];
        public TaskCompletionSource<bool> FirstSaveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseFirstSave { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AppSettings> LoadAsync(
            string dataDirectory,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppSettings());

        public async Task SaveAsync(
            string dataDirectory,
            AppSettings settings,
            CancellationToken cancellationToken = default)
        {
            var currentSave = Interlocked.Increment(ref saveCount);
            if (BlockFirstSave && currentSave == 1)
            {
                FirstSaveStarted.TrySetResult(true);
                await ReleaseFirstSave.Task.WaitAsync(cancellationToken);
            }

            if (FailNextSave || (FailSecondSave && currentSave == 2))
            {
                FailNextSave = false;
                throw new IOException("simulated settings write failure");
            }

            SavedOpacityValues.Add(settings.WindowOpacity);
        }
    }
}
