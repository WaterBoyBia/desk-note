using System.IO;
using DeskNote.App.Infrastructure;
using DeskNote.App.Models;

namespace DeskNote.App.Services;

public sealed class SettingsService
{
    private readonly IDataLocator locator;
    private readonly ISettingsStore store;
    private readonly SemaphoreSlim updateGate = new(1, 1);

    public SettingsService(IDataLocator locator, ISettingsStore store)
    {
        this.locator = locator;
        this.store = store;
    }

    public AppSettings Current { get; private set; } = new();
    public string DataDirectory { get; private set; } = string.Empty;
    public DataPaths Paths => new(DataDirectory);

    public event EventHandler? Changed;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        DataDirectory = await locator.GetOrCreateAsync(cancellationToken);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(Paths.BackupsDirectory);
        Directory.CreateDirectory(Paths.LogsDirectory);
        Current = await store.LoadAsync(DataDirectory, cancellationToken);
    }

    public async Task UpdateAsync(
        Action<AppSettings> update,
        CancellationToken cancellationToken = default)
    {
        await updateGate.WaitAsync(cancellationToken);
        try
        {
            var previous = Clone(Current);
            update(Current);
            Current.Normalize();
            try
            {
                await store.SaveAsync(DataDirectory, Current, cancellationToken);
            }
            catch
            {
                Current = previous;
                throw;
            }
        }
        finally
        {
            updateGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SwitchDataDirectory(string dataDirectory)
    {
        DataDirectory = Path.GetFullPath(dataDirectory);
        Changed?.Invoke(this, EventArgs.Empty);
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
