using DeskNote.App.Models;
using DeskNote.App.Repositories;
using DeskNote.App.Services;
using Xunit;

namespace DeskNote.Tests.Services;

public sealed class TodoServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_NormalizesAndPersistsTodo()
    {
        var repository = new InMemoryTodoRepository();
        var service = new TodoService(repository, () => Now);

        var result = await service.CreateAsync("  Read  ", "  Chapter 1  ");

        Assert.Equal("Read", result.Title);
        Assert.Equal("Chapter 1", result.Note);
        Assert.Equal(Now, result.CreatedAt);
        Assert.False(result.IsCompleted);
        Assert.Single(repository.Items);
    }

    [Fact]
    public async Task CreateAsync_RejectsInvalidTitleWithoutWriting()
    {
        var repository = new InMemoryTodoRepository();
        var service = new TodoService(repository, () => Now);

        var exception = await Assert.ThrowsAsync<TodoValidationException>(
            () => service.CreateAsync(" ", null));

        Assert.Equal("待办标题不能为空。", exception.Message);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task CompleteAsync_PersistsCompletionTimestamp()
    {
        var repository = new InMemoryTodoRepository();
        var item = new TodoItem(Guid.NewGuid(), "Read", null, false, Now, Now, null);
        repository.Items.Add(item);
        var service = new TodoService(repository, () => Now.AddMinutes(5));

        await service.CompleteAsync(item.Id);

        var actual = Assert.Single(repository.Items);
        Assert.True(actual.IsCompleted);
        Assert.Equal(Now.AddMinutes(5), actual.CompletedAt);
    }

    [Fact]
    public async Task RestoreAsync_ClearsCompletionTimestamp()
    {
        var repository = new InMemoryTodoRepository();
        var item = new TodoItem(Guid.NewGuid(), "Read", null, true, Now, Now, Now);
        repository.Items.Add(item);
        var service = new TodoService(repository, () => Now.AddMinutes(5));

        await service.RestoreAsync(item.Id);

        var actual = Assert.Single(repository.Items);
        Assert.False(actual.IsCompleted);
        Assert.Null(actual.CompletedAt);
    }

    private sealed class InMemoryTodoRepository : ITodoRepository
    {
        public List<TodoItem> Items { get; } = [];

        public Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
            TodoSortDirection direction,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<TodoItem> query = Items.Where(item => !item.IsCompleted);
            query = direction == TodoSortDirection.NewestFirst
                ? query.OrderByDescending(item => item.CreatedAt)
                : query.OrderBy(item => item.CreatedAt);
            return Task.FromResult<IReadOnlyList<TodoItem>>(query.ToList());
        }

        public Task<IReadOnlyList<TodoItem>> ListCompletedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TodoItem>>(
                Items.Where(item => item.IsCompleted).OrderByDescending(item => item.CompletedAt).ToList());

        public Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task InsertAsync(TodoItem item, CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(TodoItem item, CancellationToken cancellationToken = default)
        {
            Replace(item);
            return Task.CompletedTask;
        }

        public Task SetCompletionAsync(
            Guid id,
            bool isCompleted,
            DateTimeOffset changedAt,
            CancellationToken cancellationToken = default)
        {
            var current = Items.Single(item => item.Id == id);
            Replace(current with
            {
                IsCompleted = isCompleted,
                CompletedAt = isCompleted ? changedAt : null,
                UpdatedAt = changedAt
            });
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }

        public Task DeleteCompletedAsync(CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(item => item.IsCompleted);
            return Task.CompletedTask;
        }

        private void Replace(TodoItem item)
        {
            var index = Items.FindIndex(candidate => candidate.Id == item.Id);
            Items[index] = item;
        }
    }
}
