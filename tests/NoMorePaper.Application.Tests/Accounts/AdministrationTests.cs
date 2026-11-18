using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Accounts;
using Xunit;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>What only the administrator may do: invite, list, remove, create reset links.</summary>
public sealed class AdministrationTests
{
    private static readonly DateTimeOffset s_now = new(2026, 11, 18, 12, 0, 0, 500, TimeSpan.Zero);

    private readonly FakeAccountStore _store = new();
    private readonly FakeCurrentAccount _current = new() { IsAdmin = true };
    private readonly FixedClock _clock = new(s_now);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Members_AreRefused_Everything()
    {
        _current.IsAdmin = false;
        var log = new RecordingLogger<InviteMember>();

        var invite = await new InviteMember(_store, _current, new FixedSecrets(), _clock, log).InviteAsync(Ct);
        var list = await new ListMembers(_store, _current, NullLogger<ListMembers>.Instance).ListAsync(Ct);
        var remove = await new RemoveMember(_store, _current, NullLogger<RemoveMember>.Instance).RemoveAsync(Guid.CreateVersion7(), Ct);
        var reset = await ResetLink().CreateAsync(Guid.CreateVersion7(), Ct);

        Assert.All([invite, list, remove, reset], result => Assert.Equal(AccountStatus.Forbidden, result.Status));
        Assert.Empty(_store.Calls);
        Assert.Equal([5111], log.EventIds);
    }

    [Fact]
    public async Task Invite_StoresTheHashOfTheToken_ValidForSevenDays()
    {
        var log = new RecordingLogger<InviteMember>();

        var result = await new InviteMember(_store, _current, new FixedSecrets(), _clock, log).InviteAsync(Ct);

        var expected = new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(new IssuedLink(FixedSecrets.Token, expected), result.Value);
        Assert.Equal([SecretHash.Of(FixedSecrets.Token)], _store.Hashes);
        Assert.Equal([expected], _store.Expiries);
        Assert.DoesNotContain(log.Messages, message => message.Contains(FixedSecrets.Token, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResetLink_IsValidForADay_AndOnlyForKnownMembers()
    {
        var created = await ResetLink().CreateAsync(Guid.CreateVersion7(), Ct);
        _store.KnownAccount = false;
        var unknown = await ResetLink().CreateAsync(Guid.CreateVersion7(), Ct);

        Assert.Equal(new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero), created.Value!.ExpiresAt);
        Assert.Equal(SecretHash.Of(FixedSecrets.Token), _store.Hashes[0]);
        Assert.Equal(AccountStatus.NotFound, unknown.Status);
    }

    [Fact]
    public async Task Remove_RefusesTheAdministratorItself_AndReportsUnknownMembers()
    {
        var remove = new RemoveMember(_store, _current, NullLogger<RemoveMember>.Instance);

        var self = await remove.RemoveAsync(_current.Id, Ct);
        var removed = await remove.RemoveAsync(Guid.CreateVersion7(), Ct);
        _store.KnownAccount = false;
        var unknown = await remove.RemoveAsync(Guid.CreateVersion7(), Ct);

        Assert.Equal([AccountProblem.CannotRemoveSelf], self.Problems);
        Assert.True(removed.Succeeded);
        Assert.Equal(AccountStatus.NotFound, unknown.Status);
        Assert.Equal([nameof(IAccountStore.RemoveAsync), nameof(IAccountStore.RemoveAsync)], _store.Calls);
    }

    [Fact]
    public async Task List_GivesTheAdministratorEveryAccount()
    {
        var result = await new ListMembers(_store, _current, NullLogger<ListMembers>.Instance).ListAsync(Ct);

        Assert.Equal([FakeAccountStore.Someone], result.Value);
    }

    private CreateResetLink ResetLink() =>
        new(_store, _current, new FixedSecrets(), _clock, NullLogger<CreateResetLink>.Instance);
}
