using DeskNote.App.Models;

namespace DeskNote.App.Infrastructure;

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(
        string dataDirectory,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        string dataDirectory,
        AppSettings settings,
        CancellationToken cancellationToken = default);
}
