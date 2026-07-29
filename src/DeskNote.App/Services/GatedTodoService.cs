using DeskNote.App.Models;

namespace DeskNote.App.Services;

public sealed class GatedTodoService(ITodoService inner, DataOperationGate gate) : ITodoService
{
    public Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
        TodoSortDirection direction,
        CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.ListIncompleteAsync(direction, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<TodoItem>> ListCompletedAsync(CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.ListCompletedAsync(cancellationToken), cancellationToken);

    public Task<TodoItem> CreateAsync(
        string? title,
        string? note,
        CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.CreateAsync(title, note, cancellationToken), cancellationToken);

    public Task<TodoItem> UpdateAsync(
        Guid id,
        string? title,
        string? note,
        CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.UpdateAsync(id, title, note, cancellationToken), cancellationToken);

    public Task CompleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.CompleteAsync(id, cancellationToken), cancellationToken);

    public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.RestoreAsync(id, cancellationToken), cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.DeleteAsync(id, cancellationToken), cancellationToken);

    public Task DeleteCompletedAsync(CancellationToken cancellationToken = default) =>
        gate.RunAsync(() => inner.DeleteCompletedAsync(cancellationToken), cancellationToken);
}
