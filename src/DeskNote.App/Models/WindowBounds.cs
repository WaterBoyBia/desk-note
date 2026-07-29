namespace DeskNote.App.Models;

public sealed class WindowBounds
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 600;
    public string? ScreenDeviceName { get; set; }
}
