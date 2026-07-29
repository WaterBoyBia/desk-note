using DeskNote.App.Services;
using Xunit;

namespace DeskNote.Tests.Services;

public sealed class AppLoggerTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"desk-note-{Guid.NewGuid():N}");

    [Fact]
    public async Task LogErrorAsync_DoesNotThrowWhenLogDirectoryCannotBeCreated()
    {
        Directory.CreateDirectory(root);
        var parentFile = Path.Combine(root, "not-a-directory");
        await File.WriteAllTextAsync(parentFile, "occupied");
        var logger = new AppLogger(Path.Combine(parentFile, "app.log"));

        var exception = await Record.ExceptionAsync(() =>
            logger.LogErrorAsync(new InvalidOperationException("failure"), "test-operation"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task LogErrorAsync_RotatesBeforeTheNewEntryExceedsTheLimit()
    {
        Directory.CreateDirectory(root);
        var logFile = Path.Combine(root, "app.log");
        await File.WriteAllBytesAsync(logFile, new byte[1_048_575]);
        var logger = new AppLogger(logFile);

        await logger.LogErrorAsync(
            new InvalidOperationException("failure"),
            "test-operation");

        Assert.True(File.Exists($"{logFile}.1"));
        Assert.True(new FileInfo(logFile).Length < 1_048_576);
    }

    [Fact]
    public async Task LogErrorAsync_BoundsAndKeepsTheEntryOnOneLine()
    {
        Directory.CreateDirectory(root);
        var logFile = Path.Combine(root, "app.log");
        var logger = new AppLogger(logFile);
        var message = $"first\tline{Environment.NewLine}{new string('x', 2_000_000)}";

        await logger.LogErrorAsync(
            new InvalidOperationException(message),
            "test-operation");

        Assert.True(new FileInfo(logFile).Length < 1_048_576);
        Assert.Single(await File.ReadAllLinesAsync(logFile));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
