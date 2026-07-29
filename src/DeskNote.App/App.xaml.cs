using System.IO;
using System.Windows;
using DeskNote.App.Infrastructure;
using DeskNote.App.Repositories;
using DeskNote.App.Services;
using DeskNote.App.ViewModels;
using AppMainWindow = DeskNote.App.Views.MainWindow;
using AppRecoveryWindow = DeskNote.App.Views.RecoveryWindow;

namespace DeskNote.App;

public partial class App : System.Windows.Application
{
    private readonly CancellationTokenSource shutdownToken = new();
    private SingleInstanceService? singleInstance;
    private TrayIconController? trayIcon;
    private AppMainWindow? mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var fallbackLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "desk-note",
            "logs",
            "app.log");
        var logger = new AppLogger(fallbackLog);

        try
        {
            var instance = new SingleInstanceService();
            singleInstance = instance;
            if (!instance.IsPrimary)
            {
                try
                {
                    await instance.SignalPrimaryAsync();
                }
                finally
                {
                    Shutdown();
                }

                return;
            }

            var locator = new JsonDataLocator();
            var settingsStore = new JsonSettingsStore();
            var settings = new SettingsService(locator, settingsStore);
            await settings.InitializeAsync();
            logger = new AppLogger(Path.Combine(settings.Paths.LogsDirectory, "app.log"));

            var themeService = new ThemeService();
            themeService.Apply(settings.Current.Theme);
            if (File.Exists(settings.Paths.DatabaseFile)
                && !await DatabaseInitializer.IsHealthyAsync(settings.Paths.DatabaseFile))
            {
                var recovery = new AppRecoveryWindow(
                    new RecoveryService(new DatabaseVerifier()),
                    settings.Paths.DatabaseFile,
                    settings.Paths.BackupsDirectory);
                if (recovery.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            await DatabaseInitializer.InitializeAsync(settings.Paths.DatabaseFile);
            var gate = new DataOperationGate();
            var repository = new SqliteTodoRepository(() => settings.Paths.DatabaseFile);
            ITodoService todoService = new GatedTodoService(new TodoService(repository), gate);
            var migrationService = new DataMigrationService(
                settings,
                locator,
                gate,
                new FileSystemFacade(),
                new DatabaseVerifier());

            var todoList = new TodoListViewModel(todoService, new ConfirmationService())
            {
                SortDirection = settings.Current.IncompleteSortDirection
            };
            todoList.SortDirectionChanged += async (_, direction) =>
            {
                try
                {
                    await settings.UpdateAsync(value => value.IncompleteSortDirection = direction);
                }
                catch (Exception exception)
                {
                    await logger.LogErrorAsync(exception, "save-sort-setting");
                }
            };

            var startupService = new StartupService();
            if (startupService.IsEnabled() != settings.Current.StartWithWindows)
            {
                try
                {
                    startupService.SetEnabled(settings.Current.StartWithWindows);
                }
                catch (Exception exception)
                {
                    await logger.LogErrorAsync(exception, "reconcile-startup-setting");
                }
            }

            var editor = new TodoEditorViewModel(todoService);
            var settingsViewModel = new SettingsViewModel(
                settings,
                themeService,
                startupService,
                migrationService);
            var mainViewModel = new MainViewModel(editor, todoList, settingsViewModel);

            var window = new AppMainWindow
            {
                DataContext = mainViewModel,
                Topmost = settings.Current.AlwaysOnTop
            };
            mainWindow = window;
            var windowState = new WindowStateService();
            windowState.Restore(window, settings.Current.WindowBounds);
            window.Hiding += async (_, _) =>
            {
                try
                {
                    await settings.UpdateAsync(value => value.WindowBounds = windowState.Capture(window));
                }
                catch (Exception exception)
                {
                    await logger.LogErrorAsync(exception, "save-window-state");
                }
            };

            trayIcon = new TrayIconController(
                () => Dispatcher.Invoke(window.ShowAndActivate),
                () => Dispatcher.Invoke(() =>
                {
                    editor.BeginCreate();
                    mainViewModel.CurrentPage = Models.NavigationPage.Create;
                    window.ShowAndActivate();
                }),
                () => Dispatcher.Invoke(() =>
                {
                    window.ExitApplication();
                    Shutdown();
                }));

            instance.ShowRequested += (_, _) => Dispatcher.Invoke(window.ShowAndActivate);
            _ = ListenAndLogAsync(logger);
            await todoList.LoadCommand.ExecuteAsync(null);
            window.Show();
        }
        catch (Exception exception)
        {
            await logger.LogErrorAsync(exception, "application-startup");
            System.Windows.MessageBox.Show(
                "desk-note 启动失败。详细信息已写入本地错误日志。",
                "desk-note",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private async Task ListenAndLogAsync(AppLogger logger)
    {
        try
        {
            await singleInstance!.ListenAsync(shutdownToken.Token);
        }
        catch (Exception exception)
        {
            await logger.LogErrorAsync(exception, "single-instance-listener");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        shutdownToken.Cancel();
        trayIcon?.Dispose();
        singleInstance?.Dispose();
        shutdownToken.Dispose();
        base.OnExit(e);
    }
}
