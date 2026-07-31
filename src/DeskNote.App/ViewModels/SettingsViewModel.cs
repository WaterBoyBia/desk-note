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
    private readonly TimeSpan windowOpacitySaveDelay;
    private readonly SemaphoreSlim windowOpacitySaveGate = new(1, 1);
    private CancellationTokenSource? windowOpacityDebounceCancellation;
    private long windowOpacityVersion;
    private double lastPersistedWindowOpacity;
    private bool suppressWindowOpacitySave;

    public SettingsViewModel(
        SettingsService settings,
        ThemeService themeService,
        IStartupService startupService,
        IDataMigrationService migrationService,
        TimeSpan? windowOpacitySaveDelay = null)
    {
        this.settings = settings;
        this.themeService = themeService;
        this.startupService = startupService;
        this.migrationService = migrationService;
        this.windowOpacitySaveDelay = windowOpacitySaveDelay ?? TimeSpan.FromMilliseconds(300);
        Theme = settings.Current.Theme;
        AlwaysOnTop = settings.Current.AlwaysOnTop;
        StartWithWindows = settings.Current.StartWithWindows;
        windowOpacity = settings.Current.WindowOpacity;
        lastPersistedWindowOpacity = settings.Current.WindowOpacity;
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
    private double windowOpacity;

    public string WindowOpacityPercent => WindowOpacity.ToString("P0");
    public bool IsLowOpacity => WindowOpacity < 0.40;
    internal Task PendingWindowOpacitySave { get; private set; } = Task.CompletedTask;

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

    partial void OnWindowOpacityChanged(double value)
    {
        OnPropertyChanged(nameof(WindowOpacityPercent));
        OnPropertyChanged(nameof(IsLowOpacity));
        if (suppressWindowOpacitySave)
        {
            return;
        }

        ErrorMessage = null;
        var version = Interlocked.Increment(ref windowOpacityVersion);
        var cancellation = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref windowOpacityDebounceCancellation, cancellation);
        previous?.Cancel();
        PendingWindowOpacitySave = SaveWindowOpacityAfterDelayAsync(value, version, cancellation);
    }

    private async Task SaveWindowOpacityAfterDelayAsync(
        double value,
        long version,
        CancellationTokenSource cancellation)
    {
        try
        {
            try
            {
                await Task.Delay(windowOpacitySaveDelay, cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                return;
            }

            await windowOpacitySaveGate.WaitAsync();
            try
            {
                try
                {
                    await settings.UpdateAsync(current => current.WindowOpacity = value);
                    lastPersistedWindowOpacity = value;
                }
                catch (Exception)
                {
                    if (version == Volatile.Read(ref windowOpacityVersion))
                    {
                        suppressWindowOpacitySave = true;
                        try
                        {
                            WindowOpacity = lastPersistedWindowOpacity;
                        }
                        finally
                        {
                            suppressWindowOpacitySave = false;
                        }

                        ErrorMessage = "无法保存窗口透明度。";
                    }
                }
            }
            finally
            {
                windowOpacitySaveGate.Release();
            }
        }
        finally
        {
            Interlocked.CompareExchange(
                ref windowOpacityDebounceCancellation,
                null,
                cancellation);
            cancellation.Dispose();
        }
    }
}
