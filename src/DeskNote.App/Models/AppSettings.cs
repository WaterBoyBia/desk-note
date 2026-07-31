namespace DeskNote.App.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public const double MinimumWindowOpacity = 0.20;
    public const double MaximumWindowOpacity = 1.00;
    public const double DefaultWindowOpacity = 1.00;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ThemeMode Theme { get; set; } = ThemeMode.Light;
    public bool StartWithWindows { get; set; }
    public bool AlwaysOnTop { get; set; }
    public double WindowOpacity { get; set; } = DefaultWindowOpacity;
    public WindowBounds WindowBounds { get; set; } = new();
    public TodoSortDirection IncompleteSortDirection { get; set; } = TodoSortDirection.NewestFirst;

    public void Normalize()
    {
        WindowOpacity = double.IsFinite(WindowOpacity)
            ? Math.Clamp(WindowOpacity, MinimumWindowOpacity, MaximumWindowOpacity)
            : DefaultWindowOpacity;
    }
}
