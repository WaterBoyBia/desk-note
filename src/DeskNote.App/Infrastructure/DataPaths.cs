using System.IO;

namespace DeskNote.App.Infrastructure;

public sealed record DataPaths(string DataDirectory)
{
    public string DatabaseFile => Path.Combine(DataDirectory, "todos.db");
    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public string BackupsDirectory => Path.Combine(DataDirectory, "backups");
    public string LogsDirectory => Path.Combine(DataDirectory, "logs");
}
