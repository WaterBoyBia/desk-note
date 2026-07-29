using DeskNote.App.Models;
using DeskNote.App.Repositories;
using DeskNote.App.Validation;

namespace DeskNote.App.Services;

public sealed class TodoValidationException(string message) : Exception(message);

public sealed class TodoService : ITodoService
{
    private readonly ITodoRepository repository;
    private readonly Func<DateTimeOffset> utcNow;

    public TodoService(ITodoRepository repository, Func<DateTimeOffset>? utcNow = null)
    {
        this.repository = repository;
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
        TodoSortDirection direction,
        CancellationToken cancellationToken = default) =>
        repository.ListIncompleteAsync(direction, cancellationToken);

    public Task<IReadOnlyList<TodoItem>> ListCompletedAsync(
        CancellationToken cancellationToken = default) =>
        repository.ListCompletedAsync(cancellationToken);

    public async Task<TodoItem> CreateAsync(
        string? title,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var input = RequireValid(title, note);
        var now = utcNow();
        var item = new TodoItem(Guid.NewGuid(), input.Title, input.Note, false, now, now, null);
        await repository.InsertAsync(item, cancellationToken);
        return item;
    }

    public async Task<TodoItem> UpdateAsync(
        Guid id,
        string? title,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var input = RequireValid(title, note);
        var current = await RequireExistingAsync(id, cancellationToken);
        var updated = current with { Title = input.Title, Note = input.Note, UpdatedAt = utcNow() };
        await repository.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    public async Task CompleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var current = await RequireExistingAsync(id, cancellationToken);
        if (current.IsCompleted)
        {
            return;
        }

        await repository.SetCompletionAsync(id, true, utcNow(), cancellationToken);
    }

    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var current = await RequireExistingAsync(id, cancellationToken);
        if (!current.IsCompleted)
        {
            return;
        }

        await repository.SetCompletionAsync(id, false, utcNow(), cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(id, cancellationToken);

    public Task DeleteCompletedAsync(CancellationToken cancellationToken = default) =>
        repository.DeleteCompletedAsync(cancellationToken);

    private async Task<TodoItem> RequireExistingAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken)
        ?? throw new KeyNotFoundException($"Todo {id:D} was not found.");

    private static TodoInput RequireValid(string? title, string? note)
    {
        var result = TodoInputValidator.Validate(title, note);
        if (!result.IsValid)
        {
            throw new TodoValidationException(result.ErrorMessage!);
        }

        return new TodoInput(result.Title, result.Note);
    }
}
