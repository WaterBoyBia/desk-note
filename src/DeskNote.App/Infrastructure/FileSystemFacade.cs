using System.IO;

namespace DeskNote.App.Infrastructure;

public interface IFileSystemFacade
{
    void EnsureEmptyTarget(string targetDirectory);
    void CopyDirectory(string sourceDirectory, string targetDirectory);
    void MoveDirectory(string sourceDirectory, string targetDirectory);
}

public sealed class FileSystemFacade : IFileSystemFacade
{
    public void EnsureEmptyTarget(string targetDirectory)
    {
        if (!Directory.Exists(targetDirectory))
        {
            return;
        }

        if (Directory.EnumerateFileSystemEntries(targetDirectory).Any())
        {
            throw new IOException("目标数据目录必须为空。");
        }

        Directory.Delete(targetDirectory);
    }

    public void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var destination = Path.Combine(targetDirectory, Path.GetRelativePath(sourceDirectory, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

    public void MoveDirectory(string sourceDirectory, string targetDirectory) =>
        Directory.Move(sourceDirectory, targetDirectory);
}
