using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Accounts;
using Xunit;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>Using links and codes: only hashes reach the store, empty input never does.</summary>
public sealed class LinkUseTests
{
    private readonly FakeAccountStore _store = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EmptyTokens_AreNotFound_WithoutAskingTheStore(string? token)
    {
        var accept = new AcceptInvitation(_store, _clock, NullLogger<AcceptInvitation>.Instance);
        var reset = new ResetPassword(_store, _clock, NullLogger<ResetPassword>.Instance);

        Assert.Null(await accept.CheckAsync(token, Ct));
        Assert.Equal(AccountStatus.NotFound, (await accept.AcceptAsync(token, "anna", "Anna", "x", Ct)).Status);
        Assert.Equal(AccountStatus.NotFound, (await reset.ResetAsync(token, "x", Ct)).Status);
        Assert.Empty(_store.Calls);
    }

    [Fact]
    public async Task Tokens_ReachTheStoreOnlyAsHashes()
    {
        var accept = new AcceptInvitation(_store, _clock, NullLogger<AcceptInvitation>.Instance);
        var reset = new ResetPassword(_store, _clock, NullLogger<ResetPassword>.Instance);

        await accept.CheckAsync("einladung", Ct);
        var accepted = await accept.AcceptAsync("einladung", " anna ", "Anna", "lange-passphrase", Ct);
        var wasReset = await reset.ResetAsync("ruecksetzen", "lange-passphrase", Ct);

        Assert.True(accepted.Succeeded && wasReset.Succeeded);
        Assert.Equal([SecretHash.Of("einladung"), SecretHash.Of("einladung"), SecretHash.Of("ruecksetzen")], _store.Hashes);
    }

    [Fact]
    public async Task Accept_WithInvalidInput_LeavesTheInvitationAlone()
    {
        var accept = new AcceptInvitation(_store, _clock, NullLogger<AcceptInvitation>.Instance);

        var result = await accept.AcceptAsync("einladung", "anna", "", "lange-passphrase", Ct);

        Assert.Equal([AccountProblem.DisplayNameInvalid], result.Problems);
        Assert.Empty(_store.Calls);
    }

    [Fact]
    public async Task EmergencyReset_NormalizesTheCode_AndIssuesANewOne()
    {
        var log = new RecordingLogger<ResetWithEmergencyCode>();

        var result = await new ResetWithEmergencyCode(_store, new FixedSecrets(), log)
            .ResetAsync(" anna ", "abcde-fghij-klmno-pqrst", "lange-passphrase", Ct);

        Assert.Equal("CODE0000000000000001", result.Value);
        Assert.Equal([$"{nameof(IAccountStore.ResetWithEmergencyCodeAsync)}:anna"], _store.Calls);
        Assert.Equal([SecretHash.Of("ABCDEFGHIJKLMNOPQRST"), SecretHash.Of("CODE0000000000000001")], _store.Hashes);
        Assert.Equal([5107], log.EventIds);
        Assert.DoesNotContain(log.Messages, message => message.Contains("CODE", StringComparison.Ordinal) || message.Contains("anna", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("", "ABCDEFGHIJKLMNOPQRST")]
    [InlineData("anna", " - ")]
    public async Task EmergencyReset_WithoutNameOrCode_IsRefused(string userName, string code)
    {
        var log = new RecordingLogger<ResetWithEmergencyCode>();

        var result = await new ResetWithEmergencyCode(_store, new FixedSecrets(), log).ResetAsync(userName, code, "lange-passphrase", Ct);

        Assert.Equal(AccountStatus.Unauthorized, result.Status);
        Assert.Empty(_store.Calls);
        Assert.Equal([5108], log.EventIds);
    }

    [Fact]
    public async Task EmergencyReset_WithAWrongCode_IsRefused_AndLogged()
    {
        _store.ResetResult = AccountResult.Fail<Guid>(AccountStatus.Unauthorized);
        var log = new RecordingLogger<ResetWithEmergencyCode>();

        var result = await new ResetWithEmergencyCode(_store, new FixedSecrets(), log).ResetAsync("anna", "WRONG", "lange-passphrase", Ct);

        Assert.Equal(AccountStatus.Unauthorized, result.Status);
        Assert.Equal([5108], log.EventIds);
    }
}
