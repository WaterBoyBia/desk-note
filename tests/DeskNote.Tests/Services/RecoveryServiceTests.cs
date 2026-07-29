using DeskNote.App.Repositories;
using DeskNote.App.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeskNote.Tests.Services;

public sealed class RecoveryServiceTests : IAsyncLifetime
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"desk-note-{Guid.NewGuid():N}");

    [Fact]
    public async Task RestoreAsync_ReplacesCurrentDatabaseWithVerifiedBackup()
    {
        var current = Path.Combine(root, "todos.db");
        var backup = Path.Combine(root, "backups", "migration-1", "todos.db");
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        await File.WriteAllTextAsync(current, "broken");
        await DatabaseInitializer.InitializeAsync(backup);
        var service = new RecoveryService(new DatabaseVerifier());

        await service.RestoreAsync(backup, current);

        Assert.True(await DatabaseInitializer.IsHealthyAsync(current));
        Assert.Single(Directory.GetFiles(root, "todos.failed-*.db"));
    }

    [Fact]
    public async Task RestoreAsync_IsolatesExistingSqliteSidecarsWithTheFailedDatabase()
    {
        var current = Path.Combine(root, "todos.db");
        var backup = Path.Combine(root, "backups", "migration-1", "todos.db");
        Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
        await File.WriteAllTextAsync(current, "broken");
        await DatabaseInitializer.InitializeAsync(backup);
        string[] suffixes = ["-journal", "-wal", "-shm"];
        foreach (var suffix in suffixes)
        {
            await File.WriteAllTextAsync($"{current}{suffix}", $"old{suffix}");
        }

        var service = new RecoveryService(new DatabaseVerifier());
        await service.RestoreAsync(backup, current);

        var failed = Assert.Single(Directory.GetFiles(root, "todos.failed-*.db"));
        foreach (var suffix in suffixes)
        {
            Assert.False(File.Exists($"{current}{suffix}"));
            Assert.Equal($"old{suffix}", await File.ReadAllTextAsync($"{failed}{suffix}"));
        }
    }

    [Fact]
    public async Task RestoreAsync_RestoresOriginalFilesWhenFinalVerificationFails()
    {
        var current = Path.Combine(root, "todos.db");
        var backup = Path.Combine(root, "backup.db");
        await File.WriteAllTextAsync(current, "broken");
        await File.WriteAllTextAsync($"{current}-wal", "old-wal");
        await File.WriteAllTextAsync(backup, "backup");
        var service = new RecoveryService(new SequenceVerifier(true, true, false));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.RestoreAsync(backup, current));

        Assert.Equal("broken", await File.ReadAllTextAsync(current));
        Assert.Equal("old-wal", await File.ReadAllTextAsync($"{current}-wal"));
        Assert.Empty(Directory.GetFiles(root, "todos.failed-*.db"));
    }

    [Fact]
    public async Task RestoreAsync_SerializesConcurrentRestoreRequests()
    {
        var current = Path.Combine(root, "todos.db");
        var firstBackup = Path.Combine(root, "first.db");
        var secondBackup = Path.Combine(root, "second.db");
        await File.WriteAllTextAsync(current, "current");
        await File.WriteAllTextAsync(firstBackup, "first");
        await File.WriteAllTextAsync(secondBackup, "second");
        var verifier = new TrackingVerifier();
        var service = new RecoveryService(verifier);

        await Task.WhenAll(
            service.RestoreAsync(firstBackup, current),
            service.RestoreAsync(secondBackup, current));

        Assert.Equal(1, verifier.MaxConcurrency);
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsFalseForCorruptedDatabase()
    {
        var database = Path.Combine(root, "corrupted.db");
        await File.WriteAllTextAsync(database, "not-a-database");

        var result = await DatabaseInitializer.IsHealthyAsync(database);

        Assert.False(result);
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsFalseForZeroLengthDatabase()
    {
        var database = Path.Combine(root, "empty.db");
        await File.WriteAllBytesAsync(database, []);

        var result = await DatabaseInitializer.IsHealthyAsync(database);

        Assert.False(result);
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsFalseWhenTodoSchemaIsMissing()
    {
        var database = Path.Combine(root, "other-schema.db");
        await using (var connection = new SqliteConnection($"Data Source={database};Pooling=False"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Other (Id INTEGER PRIMARY KEY);";
            await command.ExecuteNonQueryAsync();
        }

        var result = await DatabaseInitializer.IsHealthyAsync(database);

        Assert.False(result);
    }

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(root);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        return Task.CompletedTask;
    }

    private sealed class SequenceVerifier(params bool[] results) : IDatabaseVerifier
    {
        private int index;

        public Task<bool> IsHealthyAsync(
            string databaseFile,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(results[index++]);
    }

    private sealed class TrackingVerifier : IDatabaseVerifier
    {
        private int activeChecks;
        private int maxConcurrency;

        public int MaxConcurrency => Volatile.Read(ref maxConcurrency);

        public async Task<bool> IsHealthyAsync(
            string databaseFile,
            CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref activeChecks);
            UpdateMaximum(active);
            try
            {
                await Task.Delay(20, cancellationToken);
                return true;
            }
            finally
            {
                Interlocked.Decrement(ref activeChecks);
            }
        }

        private void UpdateMaximum(int candidate)
        {
            var current = Volatile.Read(ref maxConcurrency);
            while (candidate > current)
            {
                var observed = Interlocked.CompareExchange(
                    ref maxConcurrency,
                    candidate,
                    current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }
}
