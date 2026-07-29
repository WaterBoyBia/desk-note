using DeskNote.App.Services;
using Xunit;

namespace DeskNote.Tests.Services;

public sealed class RecoveryOperationGuardTests
{
    [Fact]
    public void TryBegin_BlocksReentryUntilTheOperationCompletes()
    {
        var guard = new RecoveryOperationGuard();

        Assert.True(guard.TryBegin());
        Assert.True(guard.IsActive);
        Assert.False(guard.TryBegin());

        guard.Complete();

        Assert.False(guard.IsActive);
        Assert.True(guard.TryBegin());
    }
}
