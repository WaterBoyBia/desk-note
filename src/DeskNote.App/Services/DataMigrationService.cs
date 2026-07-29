using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeskNote.App.Infrastructure;
using DeskNote.App.Models;
using DeskNote.App.Repositories;

namespace DeskNote.App.Services;

public interface IDatabaseVerifier
{
    Task<bool> IsHealthyAsync(string databaseFile, CancellationToken cancellationToken = default);
}

public sealed class DatabaseVerifier : IDatabaseVerifier
{
    public Task<bool> IsHealthyAsync(string databaseFile, CancellationToken cancellationToken = default) =>
        DatabaseInitializer.IsHealthyAsync(databaseFile, cancellationToken);
}

public sealed record DataMigrationResult(bool Succeeded, string? ErrorMessage)
{
    public static DataMigrationResult Success() => new(true, null);
    public static DataMigrationResult Failure(string message) => new(false, message);
}

public sealed class DataMigrationService
{
    private readonly SettingsService settings;
    private readonly IDataLocator locator;
    private readonly DataOperationGate gate;
    private readonly IFileSystemFacade fileSystem;
    private readonly IDatabaseVerifier databaseVerifier;

    public DataMigrationService(
        SettingsService settings,
        IDataLocator locator,
        DataOperationGate gate,
        IFileSystemFacade fileSystem,
        IDatabaseVerifier databaseVerifier)
    {
        this.settings = settings;
        this.locator = locator;
        this.gate = gate;
        this.fileSystem = fileSystem;
        this.databaseVerifier = databaseVerifier;
    }

    public async Task<DataMigrationResult> MigrateAsync(
        string targetDirectory,
        CancellationToken cancellationToken = default)
    {
        var current = Path.GetFullPath(settings.DataDirectory);
        var target = Path.GetFullPath(targetDirectory);
        if (StringComparer.OrdinalIgnoreCase.Equals(
            current.TrimEnd(Path.DirectorySeparatorChar),
            target.TrimEnd(Path.DirectorySeparatorChar)))
        {
            return DataMigrationResult.Success();
        }

        if (target.StartsWith(current + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return DataMigrationResult.Failure("新数据目录不能位于当前数据目录内部。");
        }

        var staging = $"{target}.desk-note-staging-{Guid.NewGuid():N}";
        await using var pause = await gate.PauseAsync(cancellationToken);
        try
        {
            EnsureCapacity(current, target);
            fileSystem.EnsureEmptyTarget(target);
            CreateBackup(current);
            fileSystem.CopyDirectory(current, staging);

            if (!await databaseVerifier.IsHealthyAsync(
                Path.Combine(staging, "todos.db"),
                cancellationToken))
            {
                throw new InvalidDataException("迁移后的数据库完整性检查失败。");
            }

            var settingsFile = Path.Combine(staging, "settings.json");
            if (!File.Exists(settingsFile))
            {
                throw new InvalidDataException("迁移后的设置文件不存在。");
            }

            var settingsJson = await File.ReadAllTextAsync(settingsFile, cancellationToken);
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            if (JsonSerializer.Deserialize<AppSettings>(settingsJson, jsonOptions) is null)
            {
                throw new InvalidDataException("迁移后的设置文件内容为空。");
            }

            fileSystem.MoveDirectory(staging, target);
            await locator.SetAsync(target, cancellationToken);
            settings.SwitchDataDirectory(target);

            if (!await databaseVerifier.IsHealthyAsync(settings.Paths.DatabaseFile, cancellationToken))
            {
                throw new InvalidDataException("切换后的数据库无法打开。");
            }

            return DataMigrationResult.Success();
        }
        catch (Exception exception)
        {
            try
            {
                await locator.SetAsync(current, CancellationToken.None);
            }
            catch
            {
                // Preserve the original exception for the recovery workflow.
            }

            settings.SwitchDataDirectory(current);

            return DataMigrationResult.Failure(exception.Message);
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, true);
            }
        }
    }

    private static void CreateBackup(string currentDirectory)
    {
        var backupDirectory = Path.Combine(
            currentDirectory,
            "backups",
            $"migration-{DateTime.UtcNow:yyyyMMddHHmmssfff}");
        Directory.CreateDirectory(backupDirectory);
        foreach (var fileName in new[] { "todos.db", "settings.json" })
        {
            var source = Path.Combine(currentDirectory, fileName);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(backupDirectory, fileName));
            }
        }
    }

    private static void EnsureCapacity(string currentDirectory, string targetDirectory)
    {
        var requiredBytes = Directory.EnumerateFiles(currentDirectory, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);
        var root = Path.GetPathRoot(targetDirectory)
            ?? throw new IOException("无法确定目标磁盘。");
        var drive = new DriveInfo(root);
        const long safetyMargin = 10 * 1024 * 1024;
        if (drive.AvailableFreeSpace < requiredBytes + safetyMargin)
        {
            throw new IOException("目标磁盘可用空间不足。");
        }
    }
}
