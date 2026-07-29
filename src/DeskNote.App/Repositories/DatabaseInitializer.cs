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
        if (!File.Exists(databaseFile) || new FileInfo(databaseFile).Length == 0)
        {
            return false;
        }

        try
        {
            await using var connection = new SqliteConnection(BuildReadOnlyConnectionString(databaseFile));
            await connection.OpenAsync(cancellationToken);

            var integrityCommand = connection.CreateCommand();
            integrityCommand.CommandText = "PRAGMA quick_check;";
            if (!string.Equals(
                    (string?)await integrityCommand.ExecuteScalarAsync(cancellationToken),
                    "ok",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "PRAGMA user_version;";
            if (Convert.ToInt32(await versionCommand.ExecuteScalarAsync(cancellationToken)) != 1)
            {
                return false;
            }

            var schemaCommand = connection.CreateCommand();
            schemaCommand.CommandText = """
                SELECT COUNT(*)
                FROM pragma_table_info('Todos')
                WHERE name IN (
                    'Id', 'Title', 'Note', 'IsCompleted',
                    'CreatedAt', 'UpdatedAt', 'CompletedAt');
                """;
            return Convert.ToInt32(await schemaCommand.ExecuteScalarAsync(cancellationToken)) == 7;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    internal static string BuildConnectionString(string databaseFile) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();

    private static string BuildReadOnlyConnectionString(string databaseFile) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
            Pooling = false
        }.ToString();
}
