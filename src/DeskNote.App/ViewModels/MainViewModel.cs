using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskNote.App.Models;

namespace DeskNote.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public MainViewModel(TodoEditorViewModel editor, TodoListViewModel todoList)
    {
        Editor = editor;
        TodoList = todoList;
    }

    public TodoEditorViewModel Editor { get; }
    public TodoListViewModel TodoList { get; }

    [ObservableProperty]
    private NavigationPage currentPage = NavigationPage.Incomplete;

    [RelayCommand]
    private void Navigate(NavigationPage page) => CurrentPage = page;
}
