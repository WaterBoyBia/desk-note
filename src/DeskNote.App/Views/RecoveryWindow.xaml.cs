using System.ComponentModel;
using System.Windows;
using DeskNote.App.Services;

namespace DeskNote.App.Views;

public partial class RecoveryWindow : Window
{
    private readonly RecoveryService recoveryService;
    private readonly string currentDatabase;
    private readonly RecoveryOperationGuard operationGuard = new();

    public RecoveryWindow(
        RecoveryService recoveryService,
        string currentDatabase,
        string backupsDirectory)
    {
        InitializeComponent();
        this.recoveryService = recoveryService;
        this.currentDatabase = currentDatabase;
        BackupList.ItemsSource = recoveryService.ListBackups(backupsDirectory);
    }

    private async void OnRestore(object sender, RoutedEventArgs e)
    {
        if (operationGuard.IsActive)
        {
            return;
        }

        if (BackupList.SelectedItem is not string backup)
        {
            System.Windows.MessageBox.Show("请先选择一个备份。", "desk-note");
            return;
        }

        if (System.Windows.MessageBox.Show(
            "确定使用所选备份恢复吗？",
            "desk-note",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (!operationGuard.TryBegin())
        {
            return;
        }

        SetIsRestoring(true);
        var restored = false;
        try
        {
            await recoveryService.RestoreAsync(backup, currentDatabase);
            restored = true;
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                exception.Message,
                "恢复失败",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            operationGuard.Complete();
            SetIsRestoring(false);
        }

        if (restored)
        {
            DialogResult = true;
        }
    }

    private void OnExit(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = operationGuard.IsActive;
    }

    private void SetIsRestoring(bool isRestoring)
    {
        BackupList.IsEnabled = !isRestoring;
        ExitButton.IsEnabled = !isRestoring;
        RestoreButton.IsEnabled = !isRestoring;
    }
}
