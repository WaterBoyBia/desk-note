using DeskNote.App.Models;

namespace DeskNote.App.Services;

public interface ITodoService
{
    Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
        TodoSortDirection direction,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TodoItem>> ListCompletedAsync(CancellationToken cancellationToken = default);
    Task<TodoItem> CreateAsync(string? title, string? note, CancellationToken cancellationToken = default);
    Task<TodoItem> UpdateAsync(Guid id, string? title, string? note, CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteCompletedAsync(CancellationToken cancellationToken = default);
}
