using NoMorePaper.Application.Accounts;
using Xunit;

namespace NoMorePaper.Application.Tests.Accounts;

public sealed class SetUpHouseholdTests
{
    private readonly FakeAccountStore _store = new();
    private readonly RecordingLogger<SetUpHousehold> _log = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SetUp_StoresOnlyTheHashOfTheCode_AndReturnsTheCodeOnce()
    {
        var result = await SetUp("anna", "Anna", "lange-passphrase");

        var code = result.Value!.EmergencyCode;
        Assert.Equal(FakeAccountStore.Someone, result.Value.Administrator);
        Assert.Equal([SecretHash.Of(code)], _store.Hashes);
        Assert.Equal([5100], _log.EventIds);
        Assert.DoesNotContain(_log.Messages, message => message.Contains(code, StringComparison.Ordinal) || message.Contains("anna", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SetUp_OfASetUpHousehold_IsAConflict_WhateverTheInput()
    {
        _store.HasAccounts = true;

        var result = await SetUp("", "", "");

        Assert.Equal(AccountStatus.Conflict, result.Status);
        Assert.Empty(_store.Calls);
        Assert.Equal([5101], _log.EventIds);
        Assert.False(await new SetUpHousehold(_store, new FixedSecrets(), _log).IsNeededAsync(Ct));
    }

    [Fact]
    public async Task SetUp_WithInvalidInput_DoesNotReachTheStore()
    {
        var result = await SetUp(" ", "Anna", "lange-passphrase");

        Assert.Equal([AccountProblem.UserNameInvalid], result.Problems);
        Assert.Empty(_store.Calls);
    }

    [Fact]
    public async Task SetUp_ReportsWhatTheStoreRefused()
    {
        _store.SetUpResult = AccountResult.Invalid<HouseholdSetUp>(AccountProblem.PasswordTooShort);

        var result = await SetUp("anna", "Anna", "kurz");

        Assert.Equal([AccountProblem.PasswordTooShort], result.Problems);
        Assert.Empty(_log.EventIds);
    }

    [Fact]
    public async Task SetUp_RacingAnotherSetup_IsAConflict()
    {
        _store.SetUpResult = AccountResult.Fail<HouseholdSetUp>(AccountStatus.Conflict);

        var result = await SetUp("anna", "Anna", "lange-passphrase");

        Assert.Equal(AccountStatus.Conflict, result.Status);
        Assert.Equal([5101], _log.EventIds);
    }

    private Task<AccountResult<SetupCompleted>> SetUp(string userName, string displayName, string password) =>
        new SetUpHousehold(_store, new FixedSecrets(), _log).SetUpAsync(userName, displayName, password, Ct);
}
