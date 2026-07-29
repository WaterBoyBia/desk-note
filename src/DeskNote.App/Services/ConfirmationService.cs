namespace DeskNote.App.Services;

public interface IConfirmationService
{
    bool Confirm(string message, string title);
}

public sealed class ConfirmationService : IConfirmationService
{
    public bool Confirm(string message, string title) =>
        System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;
}

public sealed class AlwaysConfirmService : IConfirmationService
{
    public bool Confirm(string message, string title) => true;
}
