namespace DeskNote.App.Services;

public sealed class RecoveryOperationGuard
{
    private int isActive;

    public bool IsActive => Volatile.Read(ref isActive) != 0;

    public bool TryBegin() => Interlocked.CompareExchange(ref isActive, 1, 0) == 0;

    public void Complete() => Interlocked.Exchange(ref isActive, 0);
}
