using System.IO;
using Microsoft.Data.Sqlite;

namespace DeskNote.App.Repositories;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        string databaseFile,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databaseFile)!);
        await using var connection = new SqliteConnection(BuildConnectionString(databaseFile));
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Todos (
                Id TEXT PRIMARY KEY,
                Title TEXT NOT NULL,
                Note TEXT NULL,
                IsCompleted INTEGER NOT NULL CHECK (IsCompleted IN (0, 1)),
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                CompletedAt TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Todos_Incomplete
                ON Todos (IsCompleted, CreatedAt);
            CREATE INDEX IF NOT EXISTS IX_Todos_Completed
                ON Todos (IsCompleted, CompletedAt);
            PRAGMA user_version = 1;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<bool> IsHealthyAsync(
        string databaseFile,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(BuildConnectionString(databaseFile));
        await connection.OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        return string.Equals(
            (string?)await command.ExecuteScalarAsync(cancellationToken),
            "ok",
            StringComparison.OrdinalIgnoreCase);
    }

    internal static string BuildConnectionString(string databaseFile) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
}
