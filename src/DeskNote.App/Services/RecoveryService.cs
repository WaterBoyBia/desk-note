using System.IO;

namespace DeskNote.App.Services;

public sealed class RecoveryService(IDatabaseVerifier databaseVerifier)
{
    private static readonly string[] SqliteSidecarSuffixes = ["-journal", "-wal", "-shm"];
    private readonly SemaphoreSlim restoreGate = new(1, 1);

    public IReadOnlyList<string> ListBackups(string backupsDirectory)
    {
        if (!Directory.Exists(backupsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(backupsDirectory, "todos.db", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }

    public async Task RestoreAsync(
        string backupDatabase,
        string currentDatabase,
        CancellationToken cancellationToken = default)
    {
        await restoreGate.WaitAsync(cancellationToken);
        try
        {
            await RestoreCoreAsync(backupDatabase, currentDatabase, cancellationToken);
        }
        finally
        {
            restoreGate.Release();
        }
    }

    private async Task RestoreCoreAsync(
        string backupDatabase,
        string currentDatabase,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(backupDatabase))
        {
            throw new FileNotFoundException("所选备份不存在。", backupDatabase);
        }

        if (!await databaseVerifier.IsHealthyAsync(backupDatabase, cancellationToken))
        {
            throw new InvalidDataException("所选备份数据库未通过完整性检查。");
        }

        var directory = Path.GetDirectoryName(currentDatabase)!;
        Directory.CreateDirectory(directory);
        var temporary = $"{currentDatabase}.restore-{Guid.NewGuid():N}";
        var failed = Path.Combine(
            directory,
            $"todos.failed-{DateTime.UtcNow:yyyyMMddHHmmssfff}.db");
        var isolatedFiles = new List<(string Original, string Failed)>();
        var replacementInstalled = false;
        try
        {
            File.Copy(backupDatabase, temporary, true);
            if (!await databaseVerifier.IsHealthyAsync(temporary, cancellationToken))
            {
                throw new InvalidDataException("复制后的备份未通过完整性检查。");
            }

            try
            {
                IsolateIfPresent(currentDatabase, failed, isolatedFiles);
                foreach (var suffix in SqliteSidecarSuffixes)
                {
                    IsolateIfPresent(
                        $"{currentDatabase}{suffix}",
                        $"{failed}{suffix}",
                        isolatedFiles);
                }

                File.Move(temporary, currentDatabase, true);
                replacementInstalled = true;
                if (!await databaseVerifier.IsHealthyAsync(currentDatabase, cancellationToken))
                {
                    throw new InvalidDataException("恢复后的数据库未通过完整性检查。");
                }
            }
            catch (Exception restoreException)
            {
                try
                {
                    if (replacementInstalled)
                    {
                        DeleteDatabaseFiles(currentDatabase);
                    }

                    for (var index = isolatedFiles.Count - 1; index >= 0; index--)
                    {
                        var isolated = isolatedFiles[index];
                        if (File.Exists(isolated.Failed))
                        {
                            File.Move(isolated.Failed, isolated.Original, true);
                        }
                    }
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "数据库恢复失败，且原数据库未能完整回滚。",
                        restoreException,
                        rollbackException);
                }

                throw;
            }
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static void IsolateIfPresent(
        string original,
        string failed,
        ICollection<(string Original, string Failed)> isolatedFiles)
    {
        if (!File.Exists(original))
        {
            return;
        }

        File.Move(original, failed);
        isolatedFiles.Add((original, failed));
    }

    private static void DeleteDatabaseFiles(string databaseFile)
    {
        File.Delete(databaseFile);
        foreach (var suffix in SqliteSidecarSuffixes)
        {
            File.Delete($"{databaseFile}{suffix}");
        }
    }
}
