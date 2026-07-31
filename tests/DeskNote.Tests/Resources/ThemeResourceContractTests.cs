using System.Xml.Linq;
using Xunit;

namespace DeskNote.Tests.Resources;

public sealed class ThemeResourceContractTests
{
    private static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("Light.xaml")]
    [InlineData("Dark.xaml")]
    public void Theme_DefinesAllGlassControlResources(string themeFile)
    {
        var document = LoadTheme(themeFile);
        var keys = document.Descendants()
            .Select(element => (string?)element.Attribute(Xaml + "Key"))
            .Where(key => key is not null)
            .ToHashSet(StringComparer.Ordinal);
        var required = new[]
        {
            "WindowBrush", "FallbackWindowBrush", "NativeBackdropOverlayBrush",
            "PanelBrush", "SidebarBrush", "PrimaryBrush", "TextBrush",
            "MutedTextBrush", "DisabledTextBrush", "BorderBrush",
            "GlassSurfaceBrush", "GlassHoverBrush", "GlassPressedBrush",
            "GlassHighlightBrush", "FocusBrush", "DangerBrush",
            "DangerSurfaceBrush", "SliderTrackBrush", "SliderFillBrush",
            "SliderThumbBrush"
        };

        Assert.All(required, key => Assert.Contains(key, keys));
    }

    [Theory]
    [InlineData("TextBrush")]
    [InlineData("MutedTextBrush")]
    [InlineData("DisabledTextBrush")]
    [InlineData("DangerBrush")]
    public void DarkTheme_TextBrushesUseWhiteHue(string key)
    {
        var color = GetBrushColor(LoadTheme("Dark.xaml"), key);

        Assert.EndsWith("FFFFFF", color, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Controls_DefinesRoundedTemplatesIncludingSlider()
    {
        var controlsPath = Path.Combine(
            ProjectRoot,
            "src",
            "DeskNote.App",
            "Resources",
            "Controls.xaml");
        var xaml = File.ReadAllText(controlsPath);

        Assert.Contains("TargetType=\"{x:Type Button}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"{x:Type ToggleButton}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"{x:Type TextBox}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"{x:Type CheckBox}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("TargetType=\"{x:Type Slider}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("CornerRadius", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderBrush=\"Black\"", xaml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#FF000000", xaml, StringComparison.OrdinalIgnoreCase);
    }

    private static XDocument LoadTheme(string fileName) => XDocument.Load(
        Path.Combine(
            ProjectRoot,
            "src",
            "DeskNote.App",
            "Resources",
            "Themes",
            fileName));

    private static string GetBrushColor(XDocument document, string key)
    {
        var element = document.Descendants()
            .Single(candidate => (string?)candidate.Attribute(Xaml + "Key") == key);
        return (string?)element.Attribute("Color")
            ?? throw new InvalidOperationException($"Brush {key} has no Color attribute.");
    }
}
