using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskNote.App.Models;

namespace DeskNote.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public MainViewModel(
        TodoEditorViewModel editor,
        TodoListViewModel todoList,
        SettingsViewModel settings)
    {
        Editor = editor;
        TodoList = todoList;
        Settings = settings;
        Editor.Saved += OnEditorSaved;
    }

    public TodoEditorViewModel Editor { get; }
    public TodoListViewModel TodoList { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private NavigationPage currentPage = NavigationPage.Incomplete;

    [RelayCommand]
    private void Navigate(NavigationPage page)
    {
        if (page == NavigationPage.Create)
        {
            Editor.BeginCreate();
        }

        CurrentPage = page;
    }

    [RelayCommand]
    private void EditTodo(TodoItem item)
    {
        Editor.BeginEdit(item);
        CurrentPage = NavigationPage.Create;
    }

    private async void OnEditorSaved(object? sender, EventArgs e)
    {
        CurrentPage = NavigationPage.Incomplete;
        await TodoList.LoadCommand.ExecuteAsync(null);
    }
}
