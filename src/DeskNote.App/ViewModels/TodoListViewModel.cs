using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskNote.App.Models;
using DeskNote.App.Services;

namespace DeskNote.App.ViewModels;

public partial class TodoListViewModel : ObservableObject
{
    private readonly ITodoService todoService;

    public TodoListViewModel(ITodoService todoService)
    {
        this.todoService = todoService;
    }

    public ObservableCollection<TodoItem> IncompleteItems { get; } = [];
    public ObservableCollection<TodoItem> CompletedItems { get; } = [];

    [ObservableProperty]
    private TodoSortDirection sortDirection = TodoSortDirection.NewestFirst;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
    private bool isBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task LoadAsync() => RunAsync(ReloadAsync);

    [RelayCommand]
    private Task CompleteAsync(TodoItem item) =>
        RunAsync(async () =>
        {
            await todoService.CompleteAsync(item.Id);
            await ReloadAsync();
        });

    [RelayCommand]
    private Task RestoreAsync(TodoItem item) =>
        RunAsync(async () =>
        {
            await todoService.RestoreAsync(item.Id);
            await ReloadAsync();
        });

    [RelayCommand]
    private Task DeleteAsync(TodoItem item) =>
        RunAsync(async () =>
        {
            await todoService.DeleteAsync(item.Id);
            await ReloadAsync();
        });

    [RelayCommand]
    private Task ClearCompletedAsync() =>
        RunAsync(async () =>
        {
            await todoService.DeleteCompletedAsync();
            await ReloadAsync();
        });

    [RelayCommand]
    private Task SetSortAsync(TodoSortDirection direction) =>
        RunAsync(async () =>
        {
            SortDirection = direction;
            await ReloadAsync();
        });

    private bool CanRun() => !IsBusy;

    private async Task ReloadAsync()
    {
        var incomplete = await todoService.ListIncompleteAsync(SortDirection);
        var completed = await todoService.ListCompletedAsync();
        Replace(IncompleteItems, incomplete);
        Replace(CompletedItems, completed);
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception)
        {
            ErrorMessage = "操作失败，请重试。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void Replace(ObservableCollection<TodoItem> target, IEnumerable<TodoItem> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
