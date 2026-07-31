using System.Runtime.InteropServices;
using DeskNote.App.Models;

namespace DeskNote.App.Services;

public enum WindowMaterialState
{
    NativeMica,
    FallbackGlass
}

internal interface IWindowBackdropApi
{
    bool IsNativeMicaSupported { get; }
    bool TryEnableMica(nint windowHandle, bool darkMode);
    void DisableMica(nint windowHandle);
}

public sealed class WindowBackdropService
{
    private readonly IWindowBackdropApi api;

    public WindowBackdropService()
        : this(new DwmWindowBackdropApi())
    {
    }

    internal WindowBackdropService(IWindowBackdropApi api) => this.api = api;

    public WindowMaterialState Apply(
        nint windowHandle,
        ThemeMode theme,
        double opacity)
    {
        if (windowHandle == 0
            || opacity < AppSettings.MaximumWindowOpacity
            || !api.IsNativeMicaSupported)
        {
            if (windowHandle != 0)
            {
                api.DisableMica(windowHandle);
            }

            return WindowMaterialState.FallbackGlass;
        }

        if (api.TryEnableMica(windowHandle, theme == ThemeMode.Dark))
        {
            return WindowMaterialState.NativeMica;
        }

        api.DisableMica(windowHandle);
        return WindowMaterialState.FallbackGlass;
    }
}

internal sealed class DwmWindowBackdropApi : IWindowBackdropApi
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmWindowCornerPreferenceRound = 2;
    private const int DwmSystemBackdropTypeNone = 1;
    private const int DwmSystemBackdropTypeMainWindow = 2;

    public bool IsNativeMicaSupported =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);

    public bool TryEnableMica(nint windowHandle, bool darkMode)
    {
        if (!IsNativeMicaSupported)
        {
            return false;
        }

        try
        {
            if (DwmIsCompositionEnabled(out var compositionEnabled) != 0
                || !compositionEnabled)
            {
                return false;
            }

            var darkValue = darkMode ? 1 : 0;
            var cornerValue = DwmWindowCornerPreferenceRound;
            var backdropValue = DwmSystemBackdropTypeMainWindow;
            return DwmSetWindowAttribute(
                       windowHandle,
                       DwmwaUseImmersiveDarkMode,
                       ref darkValue,
                       sizeof(int)) == 0
                   && DwmSetWindowAttribute(
                       windowHandle,
                       DwmwaWindowCornerPreference,
                       ref cornerValue,
                       sizeof(int)) == 0
                   && DwmSetWindowAttribute(
                       windowHandle,
                       DwmwaSystemBackdropType,
                       ref backdropValue,
                       sizeof(int)) == 0;
        }
        catch (Exception exception) when (exception is DllNotFoundException
                                          or EntryPointNotFoundException
                                          or BadImageFormatException)
        {
            return false;
        }
    }

    public void DisableMica(nint windowHandle)
    {
        if (!IsNativeMicaSupported || windowHandle == 0)
        {
            return;
        }

        try
        {
            var backdropValue = DwmSystemBackdropTypeNone;
            _ = DwmSetWindowAttribute(
                windowHandle,
                DwmwaSystemBackdropType,
                ref backdropValue,
                sizeof(int));
        }
        catch (Exception exception) when (exception is DllNotFoundException
                                          or EntryPointNotFoundException
                                          or BadImageFormatException)
        {
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmIsCompositionEnabled(
        [MarshalAs(UnmanagedType.Bool)] out bool enabled);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int value,
        int valueSize);
}
