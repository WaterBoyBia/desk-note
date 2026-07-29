using System.Windows;
using DeskNote.App.ViewModels;
using Microsoft.Win32;

namespace DeskNote.App.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    public SettingsView() => InitializeComponent();

    private async void OnChooseDataDirectory(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择 desk-note 数据目录" };
        if (dialog.ShowDialog() == true && DataContext is SettingsViewModel viewModel)
        {
            await viewModel.MigrateDataCommand.ExecuteAsync(dialog.FolderName);
        }
    }
}
