using System.Globalization;
using DeskNote.App.Models;
using Microsoft.Data.Sqlite;

namespace DeskNote.App.Repositories;

public sealed class SqliteTodoRepository(string databaseFile) : ITodoRepository
{
    private readonly string connectionString = DatabaseInitializer.BuildConnectionString(databaseFile);

    public Task<IReadOnlyList<TodoItem>> ListIncompleteAsync(
        TodoSortDirection direction,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            $"WHERE IsCompleted = 0 ORDER BY CreatedAt {(direction == TodoSortDirection.NewestFirst ? "DESC" : "ASC")}",
            cancellationToken);

    public Task<IReadOnlyList<TodoItem>> ListCompletedAsync(
        CancellationToken cancellationToken = default) =>
        ListAsync("WHERE IsCompleted = 1 ORDER BY CompletedAt DESC", cancellationToken);

    public async Task<TodoItem?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Todos WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task InsertAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Todos
                (Id, Title, Note, IsCompleted, CreatedAt, UpdatedAt, CompletedAt)
            VALUES
                ($id, $title, $note, $completed, $created, $updated, $completedAt);
            """;
        AddParameters(command, item);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(TodoItem item, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Todos
            SET Title = $title, Note = $note, UpdatedAt = $updated
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$note", (object?)item.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$updated", Format(item.UpdatedAt));
        EnsureOne(await command.ExecuteNonQueryAsync(cancellationToken), item.Id);
    }

    public async Task SetCompletionAsync(
        Guid id,
        bool isCompleted,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            UPDATE Todos
            SET IsCompleted = $completed,
                CompletedAt = $completedAt,
                UpdatedAt = $updated
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$completed", isCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$completedAt", isCompleted ? Format(changedAt) : DBNull.Value);
        command.Parameters.AddWithValue("$updated", Format(changedAt));
        EnsureOne(await command.ExecuteNonQueryAsync(cancellationToken), id);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Todos WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        EnsureOne(await command.ExecuteNonQueryAsync(cancellationToken), id);
    }

    public async Task DeleteCompletedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Todos WHERE IsCompleted = 1;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<TodoItem>> ListAsync(
        string clause,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM Todos {clause};";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<TodoItem>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Read(reader));
        }

        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static void AddParameters(SqliteCommand command, TodoItem item)
    {
        command.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        command.Parameters.AddWithValue("$title", item.Title);
        command.Parameters.AddWithValue("$note", (object?)item.Note ?? DBNull.Value);
        command.Parameters.AddWithValue("$completed", item.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("$created", Format(item.CreatedAt));
        command.Parameters.AddWithValue("$updated", Format(item.UpdatedAt));
        command.Parameters.AddWithValue("$completedAt", item.CompletedAt is null ? DBNull.Value : Format(item.CompletedAt.Value));
    }

    private static TodoItem Read(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
        reader.GetString(reader.GetOrdinal("Title")),
        reader.IsDBNull(reader.GetOrdinal("Note")) ? null : reader.GetString(reader.GetOrdinal("Note")),
        reader.GetInt32(reader.GetOrdinal("IsCompleted")) == 1,
        Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
        Parse(reader.GetString(reader.GetOrdinal("UpdatedAt"))),
        reader.IsDBNull(reader.GetOrdinal("CompletedAt"))
            ? null
            : Parse(reader.GetString(reader.GetOrdinal("CompletedAt"))));

    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O");

    private static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static void EnsureOne(int rows, Guid id)
    {
        if (rows != 1)
        {
            throw new KeyNotFoundException($"Todo {id:D} was not found.");
        }
    }
}
