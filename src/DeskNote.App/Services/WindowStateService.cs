using System.Windows;
using System.Windows.Interop;
using DeskNote.App.Models;
using Forms = System.Windows.Forms;

namespace DeskNote.App.Services;

public sealed class WindowStateService
{
    public void Restore(Window window, WindowBounds bounds)
    {
        window.Width = Math.Max(360, bounds.Width);
        window.Height = Math.Max(480, bounds.Height);

        if (bounds.Left is double left && bounds.Top is double top && IsVisible(left, top, window.Width, window.Height))
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = left;
            window.Top = top;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }

    public WindowBounds Capture(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        return new WindowBounds
        {
            Left = window.RestoreBounds.Left,
            Top = window.RestoreBounds.Top,
            Width = window.RestoreBounds.Width,
            Height = window.RestoreBounds.Height,
            ScreenDeviceName = handle == IntPtr.Zero ? null : Forms.Screen.FromHandle(handle).DeviceName
        };
    }

    private static bool IsVisible(double left, double top, double width, double height)
    {
        var right = left + width;
        var bottom = top + height;
        return right > SystemParameters.VirtualScreenLeft + 100
            && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100
            && bottom > SystemParameters.VirtualScreenTop + 100
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100;
    }
}
