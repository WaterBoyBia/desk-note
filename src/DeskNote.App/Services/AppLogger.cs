using System.IO;
using System.Text;

namespace DeskNote.App.Services;

public sealed class AppLogger(string logFile)
{
    private const long MaxBytes = 1_048_576;
    private const int MaxMessageCharacters = 16_384;
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task LogErrorAsync(Exception exception, string operation)
    {
        await gate.WaitAsync();
        try
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
                var encoding = new UTF8Encoding(false);
                var message = NormalizeMessage(exception.Message);
                var line = $"{DateTimeOffset.UtcNow:O}\t{operation}\t{exception.GetType().Name}\t{message}{Environment.NewLine}";
                var entryBytes = encoding.GetByteCount(line);
                if (File.Exists(logFile)
                    && new FileInfo(logFile).Length + entryBytes > MaxBytes)
                {
                    File.Move(logFile, $"{logFile}.1", true);
                }

                await File.AppendAllTextAsync(logFile, line, encoding);
            }
            catch (Exception loggingException) when (IsExpectedLoggingFailure(loggingException))
            {
                // Logging is best-effort and must not hide the original application error.
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool IsExpectedLoggingFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Security.SecurityException;

    private static string NormalizeMessage(string message)
    {
        var normalized = message
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
        return normalized.Length <= MaxMessageCharacters
            ? normalized
            : normalized[..MaxMessageCharacters];
    }
}
