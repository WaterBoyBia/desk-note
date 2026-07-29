using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using KeyboardFocusChangedEventArgs = System.Windows.Input.KeyboardFocusChangedEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace DeskNote.App.Views;

public partial class MainWindow : Window
{
    private bool allowClose;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void ExitApplication()
    {
        allowClose = true;
        Close();
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

    private void OnHide(object sender, RoutedEventArgs e) => Hide();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!allowClose)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
