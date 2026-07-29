using DeskNote.App.Models;
using DeskNote.App.Services;
using DeskNote.App.ViewModels;
using Xunit;

namespace DeskNote.Tests.ViewModels;

public sealed class TodoListViewModelTests
{
    [Fact]
    public async Task LoadCommand_LoadsBothLists()
    {
        var service = new FakeTodoService();
        service.Incomplete.Add(CreateTodo(false));
        service.Completed.Add(CreateTodo(true));
        var viewModel = new TodoListViewModel(service);

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Single(viewModel.IncompleteItems);
        Assert.Single(viewModel.CompletedItems);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CompleteCommand_ReloadsAfterSuccessfulWrite()
    {
        var service = new FakeTodoService();
        var item = CreateTodo(false);
        service.Incomplete.Add(item);
        var viewModel = new TodoListViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.CompleteCommand.ExecuteAsync(item);

        Assert.Equal(item.Id, service.CompletedId);
        Assert.Empty(viewModel.IncompleteItems);
        Assert.Single(viewModel.CompletedItems);
    }

    [Fact]
    public async Task CompleteCommand_LeavesListAndShowsErrorWhenWriteFails()
    {
        var service = new FakeTodoService { CompletionException = new IOException("disk full") };
        var item = CreateTodo(false);
        service.Incomplete.Add(item);
        var viewModel = new TodoListViewModel(service);
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.CompleteCommand.ExecuteAsync(item);

        Assert.Single(viewModel.IncompleteItems);
        Assert.Equal("操作失败，请重试。", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task DeleteCommand_DoesNotDeleteWhenUserCancels()
    {
        var service = new FakeTodoService();
        var item = CreateTodo(false);
        service.Incomplete.Add(item);
        var viewModel = new TodoListViewModel(service, new RejectConfirmationService());
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.DeleteCommand.ExecuteAsync(item);

        Assert.Single(viewModel.IncompleteItems);
    }

    private static TodoItem CreateTodo(bool completed)
    {
        var now = DateTimeOffset.UtcNow;
        return new TodoItem(Guid.NewGuid(), "Read", null, completed, now, now, completed ? now : null);
    }

    private sealed class FakeTodoService : ITodoService
    {
        public List<TodoItem> Incomplete { get; } = [];
        public List<TodoItem> Completed { get; } = [];
        public Guid? CompletedId { get; private set; }
        public Exception? CompletionException { get; init; }

        public Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
            TodoSortDirection direction,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TodoItem>>(Incomplete.ToList());

        public Task<IReadOnlyList<TodoItem>> ListCompletedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TodoItem>>(Completed.ToList());

        public Task CompleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (CompletionException is not null)
            {
                throw CompletionException;
            }

            CompletedId = id;
            var item = Incomplete.Single(candidate => candidate.Id == id);
            Incomplete.Remove(item);
            Completed.Add(item with { IsCompleted = true, CompletedAt = DateTimeOffset.UtcNow });
            return Task.CompletedTask;
        }

        public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var item = Completed.Single(candidate => candidate.Id == id);
            Completed.Remove(item);
            Incomplete.Add(item with { IsCompleted = false, CompletedAt = null });
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Incomplete.RemoveAll(item => item.Id == id);
            Completed.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }

        public Task DeleteCompletedAsync(CancellationToken cancellationToken = default)
        {
            Completed.Clear();
            return Task.CompletedTask;
        }

        public Task<TodoItem> CreateAsync(string? title, string? note, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TodoItem> UpdateAsync(Guid id, string? title, string? note, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RejectConfirmationService : IConfirmationService
    {
        public bool Confirm(string message, string title) => false;
    }
}
