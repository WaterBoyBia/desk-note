using System.Windows;
using DeskNote.App.Infrastructure;
using DeskNote.App.Repositories;
using DeskNote.App.Services;
using DeskNote.App.ViewModels;
using AppMainWindow = DeskNote.App.Views.MainWindow;

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

        var themeService = new ThemeService();
        themeService.Apply(settings.Current.Theme);
        await DatabaseInitializer.InitializeAsync(settings.Paths.DatabaseFile);

        var gate = new DataOperationGate();
        var repository = new SqliteTodoRepository(() => settings.Paths.DatabaseFile);
        var coreTodoService = new TodoService(repository);
        ITodoService todoService = new GatedTodoService(coreTodoService, gate);
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
            await settings.UpdateAsync(value => value.IncompleteSortDirection = direction);

        var editor = new TodoEditorViewModel(todoService);
        var startupService = new StartupService();
        if (startupService.IsEnabled() != settings.Current.StartWithWindows)
        {
            try
            {
                startupService.SetEnabled(settings.Current.StartWithWindows);
            }
            catch (Exception)
            {
                // SettingsViewModel displays a warning when desired and actual state differ.
            }
        }

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
            await settings.UpdateAsync(value => value.WindowBounds = windowState.Capture(window));

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
        _ = instance.ListenAsync(shutdownToken.Token);

        await todoList.LoadCommand.ExecuteAsync(null);
        window.Show();
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
