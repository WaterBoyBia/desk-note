using DeskNote.App.Infrastructure;
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

    private sealed class NoOpMigrationService : IDataMigrationService
    {
        public Task<DataMigrationResult> MigrateAsync(
            string targetDirectory,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DataMigrationResult.Success());
    }
}
