using System.IO;
using System.IO.Pipes;
using System.Text;

namespace DeskNote.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private readonly string pipeName;
    private readonly Mutex mutex;

    public SingleInstanceService()
    {
        var identity = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(Environment.UserName)))[..12];
        pipeName = $"desk-note-{identity}";
        mutex = new Mutex(true, $@"Local\desk-note-{identity}", out var createdNew);
        IsPrimary = createdNew;
    }

    public bool IsPrimary { get; }
    public event EventHandler? ShowRequested;

    public async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await using var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync(cancellationToken);
                using var reader = new StreamReader(server, Encoding.UTF8, false, leaveOpen: true);
                if (string.Equals(
                    await reader.ReadLineAsync(cancellationToken),
                    "SHOW",
                    StringComparison.Ordinal))
                {
                    ShowRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async Task SignalPrimaryAsync(CancellationToken cancellationToken = default)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.Out,
            PipeOptions.Asynchronous,
            System.Security.Principal.TokenImpersonationLevel.Identification);
        await client.ConnectAsync(1000, cancellationToken);
        await using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true)
        {
            AutoFlush = true
        };
        await writer.WriteLineAsync("SHOW".AsMemory(), cancellationToken);
    }

    public void Dispose()
    {
        if (IsPrimary)
        {
            mutex.ReleaseMutex();
        }

        mutex.Dispose();
    }
}
