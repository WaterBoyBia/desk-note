using Microsoft.Win32;

namespace DeskNote.App.Services;

public interface IStartupService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

public sealed class StartupService : IStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "desk-note";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        var actual = key?.GetValue(ValueName) as string;
        return string.Equals(actual, QuotedExecutablePath(), StringComparison.OrdinalIgnoreCase);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true)
            ?? throw new InvalidOperationException("无法打开 Windows 启动项。");
        if (enabled)
        {
            key.SetValue(ValueName, QuotedExecutablePath(), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    private static string QuotedExecutablePath()
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定程序路径。");
        return $"\"{executable}\"";
    }
}
