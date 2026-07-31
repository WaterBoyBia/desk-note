using System.Xml.Linq;
using Xunit;

namespace DeskNote.Tests.Views;

public sealed class MainWindowTransparencyContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void MainWindow_UsesWpfTransparencyForTheWholeVisualTree()
    {
        var document = XDocument.Load(ProjectFile("Views", "MainWindow.xaml"));
        var window = Assert.IsType<XElement>(document.Root);

        Assert.Equal("None", (string?)window.Attribute("WindowStyle"));
        Assert.Equal("True", (string?)window.Attribute("AllowsTransparency"));
        Assert.Equal("Transparent", (string?)window.Attribute("Background"));

        var opacity = (string?)window.Attribute("Opacity");
        Assert.NotNull(opacity);
        Assert.Contains("Settings.WindowOpacity", opacity, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindow_DoesNotRunAMicaBackdropLifecycle()
    {
        var mainWindowCode = File.ReadAllText(ProjectFile("Views", "MainWindow.xaml.cs"));
        var appCode = File.ReadAllText(Path.Combine(ProjectRoot, "src", "DeskNote.App", "App.xaml.cs"));

        Assert.DoesNotContain("WindowBackdropService", mainWindowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyWindowMaterial", mainWindowCode, StringComparison.Ordinal);
        Assert.DoesNotContain("new WindowBackdropService()", appCode, StringComparison.Ordinal);
    }

    private static string ProjectFile(params string[] relativeSegments) =>
        Path.Combine([ProjectRoot, "src", "DeskNote.App", .. relativeSegments]);
}
