using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DeskNote.App.Models;
using DeskNote.App.Services;
using AppThemeMode = DeskNote.App.Models.ThemeMode;
using KeyboardFocusChangedEventArgs = System.Windows.Input.KeyboardFocusChangedEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace DeskNote.App.Views;

public partial class MainWindow : Window
{
    private const int WmSettingChange = 0x001A;
    private const int WmThemeChanged = 0x031A;
    private const int WmDwmCompositionChanged = 0x031E;

    private readonly ThemeService themeService;
    private readonly WindowBackdropService backdropService;
    private HwndSource? windowSource;
    private bool allowClose;

    public MainWindow(
        ThemeService themeService,
        WindowBackdropService backdropService)
    {
        this.themeService = themeService;
        this.backdropService = backdropService;
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
        themeService.Changed += OnThemeChanged;
    }

    public event EventHandler? Hiding;

    public void ExitApplication()
    {
        allowClose = true;
        Close();
    }

    public void ShowAndActivate()
    {
        Show();
        ApplyWindowMaterial();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    private void HideWindow()
    {
        Hiding?.Invoke(this, EventArgs.Empty);
        Hide();
    }

    private void OnSidebarMouseEnter(object sender, MouseEventArgs e) => AnimateSidebar(150);

    private void OnSidebarMouseLeave(object sender, MouseEventArgs e)
    {
        if (!Sidebar.IsKeyboardFocusWithin)
        {
            AnimateSidebar(58);
        }
    }

    private void OnSidebarFocusChanged(object sender, KeyboardFocusChangedEventArgs e) =>
        AnimateSidebar(Sidebar.IsKeyboardFocusWithin ? 150 : 58);

    private void AnimateSidebar(double width)
    {
        var duration = SystemParameters.ClientAreaAnimation ? TimeSpan.FromMilliseconds(180) : TimeSpan.Zero;
        Sidebar.BeginAnimation(WidthProperty, new DoubleAnimation(width, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private async void OnPinChanged(object sender, RoutedEventArgs e)
    {
        var enabled = sender is System.Windows.Controls.Primitives.ToggleButton { IsChecked: true };
        if (DataContext is ViewModels.MainViewModel viewModel)
        {
            await viewModel.Settings.SetAlwaysOnTopCommand.ExecuteAsync(enabled);
            Topmost = viewModel.Settings.AlwaysOnTop;
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnHide(object sender, RoutedEventArgs e) => HideWindow();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose)
        {
            Hiding?.Invoke(this, EventArgs.Empty);
            return;
        }

        e.Cancel = true;
        HideWindow();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == OpacityProperty && IsInitialized)
        {
            ApplyWindowMaterial();
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        windowSource?.AddHook(WindowMessageHook);
        ApplyWindowMaterial();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        themeService.Changed -= OnThemeChanged;
        windowSource?.RemoveHook(WindowMessageHook);
        windowSource = null;
    }

    private void OnThemeChanged(AppThemeMode mode) => ApplyWindowMaterial(mode);

    private nint WindowMessageHook(
        nint windowHandle,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message is WmSettingChange or WmThemeChanged or WmDwmCompositionChanged)
        {
            Dispatcher.BeginInvoke(new Action(ApplyWindowMaterial));
        }

        return 0;
    }

    private void ApplyWindowMaterial()
    {
        var theme = DataContext is ViewModels.MainViewModel viewModel
            ? viewModel.Settings.Theme
            : AppThemeMode.Light;
        ApplyWindowMaterial(theme);
    }

    private void ApplyWindowMaterial(AppThemeMode theme)
    {
        if (RootSurface is null)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        SetResourceReference(BackgroundProperty, "FallbackWindowBrush");
        RootSurface.SetResourceReference(
            System.Windows.Controls.Border.BackgroundProperty,
            "FallbackWindowBrush");
        var state = backdropService.Apply(handle, theme, Opacity);
        if (state == WindowMaterialState.NativeMica)
        {
            Background = System.Windows.Media.Brushes.Transparent;
            RootSurface.SetResourceReference(
                System.Windows.Controls.Border.BackgroundProperty,
                "NativeBackdropOverlayBrush");
        }
    }
}
