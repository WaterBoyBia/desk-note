using DeskNote.App.Models;

namespace DeskNote.App.Repositories;

public interface ITodoRepository
{
    Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
        TodoSortDirection direction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TodoItem>> ListCompletedAsync(
        CancellationToken cancellationToken = default);

    Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task InsertAsync(TodoItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(TodoItem item, CancellationToken cancellationToken = default);
    Task SetCompletionAsync(
        Guid id,
        bool isCompleted,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteCompletedAsync(CancellationToken cancellationToken = default);
}
