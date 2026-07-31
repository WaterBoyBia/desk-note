using DeskNote.App.Models;
using DeskNote.App.Services;
using Xunit;

namespace DeskNote.Tests.Services;

public sealed class WindowBackdropServiceTests
{
    [Fact]
    public void Apply_UsesFallbackWhenOpacityIsBelowOne()
    {
        var api = new FakeWindowBackdropApi { IsNativeMicaSupported = true };
        var service = new WindowBackdropService(api);

        var result = service.Apply((nint)123, ThemeMode.Dark, 0.99);

        Assert.Equal(WindowMaterialState.FallbackGlass, result);
        Assert.Equal(1, api.DisableCalls);
        Assert.Equal(0, api.EnableCalls);
    }

    [Fact]
    public void Apply_UsesNativeMicaWhenEveryRequirementSucceeds()
    {
        var api = new FakeWindowBackdropApi
        {
            IsNativeMicaSupported = true,
            EnableResult = true
        };
        var service = new WindowBackdropService(api);

        var result = service.Apply((nint)123, ThemeMode.Dark, 1.0);

        Assert.Equal(WindowMaterialState.NativeMica, result);
        Assert.True(api.LastDarkMode);
        Assert.Equal(1, api.EnableCalls);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Apply_FallsBackWhenPlatformOrNativeCallFails(
        bool isSupported,
        bool enableResult)
    {
        var api = new FakeWindowBackdropApi
        {
            IsNativeMicaSupported = isSupported,
            EnableResult = enableResult
        };
        var service = new WindowBackdropService(api);

        var result = service.Apply((nint)123, ThemeMode.Light, 1.0);

        Assert.Equal(WindowMaterialState.FallbackGlass, result);
        Assert.Equal(1, api.DisableCalls);
    }

    private sealed class FakeWindowBackdropApi : IWindowBackdropApi
    {
        public bool IsNativeMicaSupported { get; set; }
        public bool EnableResult { get; set; }
        public bool LastDarkMode { get; private set; }
        public int EnableCalls { get; private set; }
        public int DisableCalls { get; private set; }

        public bool TryEnableMica(nint windowHandle, bool darkMode)
        {
            EnableCalls++;
            LastDarkMode = darkMode;
            return EnableResult;
        }

        public void DisableMica(nint windowHandle) => DisableCalls++;
    }
}
