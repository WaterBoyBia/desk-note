using DeskNote.App.Models;
using DeskNote.App.Repositories;
using Xunit;

namespace DeskNote.Tests.Repositories;

public sealed class SqliteTodoRepositoryTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");
    private string databaseFile = string.Empty;
    private SqliteTodoRepository repository = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(root);
        databaseFile = Path.Combine(root, "todos.db");
        await DatabaseInitializer.InitializeAsync(databaseFile);
        repository = new SqliteTodoRepository(databaseFile);
    }

    [Fact]
    public async Task InsertAndListIncompleteAsync_RoundTripsTodo()
    {
        var now = DateTimeOffset.UtcNow;
        var todo = new TodoItem(Guid.NewGuid(), "Read", "Chapter 1", false, now, now, null);

        await repository.InsertAsync(todo);
        var items = await repository.ListIncompleteAsync(TodoSortDirection.NewestFirst);

        var actual = Assert.Single(items);
        Assert.Equal(todo.Id, actual.Id);
        Assert.Equal("Read", actual.Title);
        Assert.False(actual.IsCompleted);
    }

    [Fact]
    public async Task SetCompletionAsync_MovesTodoToCompletedQuery()
    {
        var now = DateTimeOffset.UtcNow;
        var todo = new TodoItem(Guid.NewGuid(), "Read", null, false, now, now, null);
        await repository.InsertAsync(todo);

        await repository.SetCompletionAsync(todo.Id, true, now.AddMinutes(1));

        Assert.Empty(await repository.ListIncompleteAsync(TodoSortDirection.NewestFirst));
        var completed = Assert.Single(await repository.ListCompletedAsync());
        Assert.True(completed.IsCompleted);
        Assert.NotNull(completed.CompletedAt);
    }

    [Fact]
    public async Task DeleteCompletedAsync_DoesNotDeleteIncompleteTodos()
    {
        var now = DateTimeOffset.UtcNow;
        var incomplete = new TodoItem(Guid.NewGuid(), "Keep", null, false, now, now, null);
        var completed = new TodoItem(Guid.NewGuid(), "Delete", null, true, now, now, now);
        await repository.InsertAsync(incomplete);
        await repository.InsertAsync(completed);

        await repository.DeleteCompletedAsync();

        Assert.Single(await repository.ListIncompleteAsync(TodoSortDirection.NewestFirst));
        Assert.Empty(await repository.ListCompletedAsync());
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }
}
