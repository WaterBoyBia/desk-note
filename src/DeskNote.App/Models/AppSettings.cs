namespace DeskNote.App.Models;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ThemeMode Theme { get; set; } = ThemeMode.Light;
    public bool StartWithWindows { get; set; }
    public bool AlwaysOnTop { get; set; }
    public WindowBounds WindowBounds { get; set; } = new();
    public TodoSortDirection IncompleteSortDirection { get; set; } = TodoSortDirection.NewestFirst;
}
