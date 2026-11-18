using NoMorePaper.Application.Accounts;
using Xunit;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>The notice about the administrator's reset links, and the member confirming it.</summary>
public sealed class AcknowledgeAdminResetTests
{
    private static readonly DateTimeOffset s_now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset s_first = s_now.AddDays(-10);
    private static readonly DateTimeOffset s_second = s_now.AddDays(-1);

    private readonly FakeAccountStore _store = new();
    private readonly FakeCurrentAccount _current = new();
    private readonly RecordingLogger<AcknowledgeAdminReset> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Acknowledge_MarksTheResetsOfTheSignedInAccount_AtNow_AndLogsOnlyItsId()
    {
        var acknowledged = await Acknowledge().AcknowledgeAsync(Ct);

        Assert.True(acknowledged);
        Assert.Equal([nameof(IAccountStore.AcknowledgeAdminResetsAsync)], _store.Calls);
        Assert.Equal([s_now], _store.Expiries);
        Assert.Equal([5112], _log.EventIds);
        Assert.Contains(_current.Id.ToString(), _log.Messages.Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Acknowledge_ByARemovedAccount_IsRefused()
    {
        _store.KnownAccount = false;

        var acknowledged = await Acknowledge().AcknowledgeAsync(Ct);

        Assert.False(acknowledged);
        Assert.Equal([5113], _log.EventIds);
    }

    [Fact]
    public void Notice_IsEmpty_WithoutResets_AndWhenEveryResetIsAcknowledged()
    {
        Assert.Equal(AdminResetNotice.None, AdminResetNotice.From([]));
        Assert.Equal(AdminResetNotice.None, AdminResetNotice.From([new AdminReset(s_first, s_first.AddHours(1))]));
    }

    [Fact]
    public void Notice_ListsAnAcknowledgedReset_AgainWhenANewOneComes()
    {
        // The administrator set the password, signed in as the member and confirmed the notice;
        // the member, locked out, asked for a new link: both resets must show.
        var notice = AdminResetNotice.From([new AdminReset(s_second, null), new AdminReset(s_first, s_first.AddMinutes(5))]);

        Assert.Equal([s_first, s_second], notice.Resets);
        Assert.Equal(s_second, notice.OldestUnacknowledged);
    }

    [Fact]
    public async Task GetNotice_ReadsTheSignedInAccountsResets()
    {
        _store.AdminResets.AddRange([new AdminReset(s_second, null), new AdminReset(s_first, null)]);

        var notice = await new GetAdminResetNotice(_store, _current).GetAsync(Ct);

        Assert.Equal([s_first, s_second], notice.Resets);
        Assert.Equal(s_first, notice.OldestUnacknowledged);
    }

    private AcknowledgeAdminReset Acknowledge() => new(_store, _current, new FixedClock(s_now), _log);
}
