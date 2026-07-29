using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskNote.App.Models;
using DeskNote.App.Services;
using DeskNote.App.Validation;

namespace DeskNote.App.ViewModels;

public partial class TodoEditorViewModel : ObservableObject
{
    private readonly ITodoService todoService;

    public TodoEditorViewModel(ITodoService todoService)
    {
        this.todoService = todoService;
    }

    public event EventHandler? Saved;

    [ObservableProperty]
    private Guid? editingId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string title = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string? note;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool isBusy;

    public void BeginCreate()
    {
        EditingId = null;
        Title = string.Empty;
        Note = null;
        ErrorMessage = null;
    }

    public void BeginEdit(TodoItem item)
    {
        EditingId = item.Id;
        Title = item.Title;
        Note = item.Note;
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (EditingId is Guid id)
            {
                await todoService.UpdateAsync(id, Title, Note);
            }
            else
            {
                await todoService.CreateAsync(Title, Note);
            }

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (TodoValidationException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "保存失败，请重试。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => BeginCreate();

    private bool CanSave() => !IsBusy && TodoInputValidator.Validate(Title, Note).IsValid;
}
