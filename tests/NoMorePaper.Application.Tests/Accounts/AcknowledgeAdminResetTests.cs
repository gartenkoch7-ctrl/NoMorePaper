using NoMorePaper.Application.Accounts;
using Xunit;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>The member confirms the notice about the administrator's reset link.</summary>
public sealed class AcknowledgeAdminResetTests
{
    private readonly FakeAccountStore _store = new();
    private readonly FakeCurrentAccount _current = new();
    private readonly RecordingLogger<AcknowledgeAdminReset> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ClearsTheNotice_OfTheSignedInAccount_AndLogsOnlyItsId()
    {
        var acknowledged = await new AcknowledgeAdminReset(_store, _current, _log).AcknowledgeAsync(Ct);

        Assert.True(acknowledged);
        Assert.Equal([nameof(IAccountStore.ClearAdminResetAsync)], _store.Calls);
        Assert.Equal([5112], _log.EventIds);
        Assert.Contains(_current.Id.ToString(), _log.Messages.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RemovedAccount_IsNotLogged()
    {
        _store.KnownAccount = false;

        var acknowledged = await new AcknowledgeAdminReset(_store, _current, _log).AcknowledgeAsync(Ct);

        Assert.False(acknowledged);
        Assert.Empty(_log.EventIds);
    }
}
