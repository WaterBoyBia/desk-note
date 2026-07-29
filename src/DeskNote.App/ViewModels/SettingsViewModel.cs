using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DeskNote.App.Models;
using DeskNote.App.Services;

namespace DeskNote.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService settings;
    private readonly ThemeService themeService;
    private readonly IStartupService startupService;
    private readonly IDataMigrationService migrationService;

    public SettingsViewModel(
        SettingsService settings,
        ThemeService themeService,
        IStartupService startupService,
        IDataMigrationService migrationService)
    {
        this.settings = settings;
        this.themeService = themeService;
        this.startupService = startupService;
        this.migrationService = migrationService;
        Theme = settings.Current.Theme;
        AlwaysOnTop = settings.Current.AlwaysOnTop;
        StartWithWindows = settings.Current.StartWithWindows;
        DataDirectory = settings.DataDirectory;
        if (startupService.IsEnabled() != StartWithWindows)
        {
            ErrorMessage = "开机自启动设置尚未在 Windows 中生效。";
        }
    }

    [ObservableProperty]
    private ThemeMode theme;

    [ObservableProperty]
    private bool startWithWindows;

    [ObservableProperty]
    private bool alwaysOnTop;

    [ObservableProperty]
    private string dataDirectory = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isMigrating;

    [RelayCommand]
    private async Task SetThemeAsync(ThemeMode mode)
    {
        ErrorMessage = null;
        try
        {
            await settings.UpdateAsync(value => value.Theme = mode);
            Theme = mode;
            themeService.Apply(mode);
        }
        catch (Exception)
        {
            ErrorMessage = "无法保存颜色模式。";
        }
    }

    [RelayCommand]
    private async Task SetAlwaysOnTopAsync(bool enabled)
    {
        ErrorMessage = null;
        try
        {
            await settings.UpdateAsync(value => value.AlwaysOnTop = enabled);
            AlwaysOnTop = enabled;
        }
        catch (Exception)
        {
            ErrorMessage = "无法保存窗口置顶设置。";
        }
    }

    [RelayCommand]
    private async Task SetStartWithWindowsAsync(bool enabled)
    {
        var previous = StartWithWindows;
        ErrorMessage = null;
        try
        {
            startupService.SetEnabled(enabled);
            try
            {
                await settings.UpdateAsync(value => value.StartWithWindows = enabled);
            }
            catch
            {
                startupService.SetEnabled(previous);
                throw;
            }

            StartWithWindows = enabled;
        }
        catch (Exception)
        {
            StartWithWindows = previous;
            ErrorMessage = "无法修改开机自启动。";
        }
    }

    [RelayCommand]
    private async Task MigrateDataAsync(string targetDirectory)
    {
        IsMigrating = true;
        ErrorMessage = null;
        try
        {
            var result = await migrationService.MigrateAsync(targetDirectory);
            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                return;
            }

            DataDirectory = settings.DataDirectory;
        }
        catch (Exception)
        {
            ErrorMessage = "无法迁移数据目录。";
        }
        finally
        {
            IsMigrating = false;
        }
    }
}
