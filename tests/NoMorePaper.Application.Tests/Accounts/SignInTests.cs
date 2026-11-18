using NoMorePaper.Application.Accounts;
using Xunit;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>Sign-in timing and the password change of a locked or removed account.</summary>
public sealed class SignInTests
{
    private readonly FakeAccountStore _store = new();
    private readonly DelayRecordingClock _clock = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RefusedSignIn_UnknownNameOrWrongPassword_WaitsForTheSameMinimum(bool knownName)
    {
        _store.CheckResult = PasswordCheck.Failure(knownName ? Guid.CreateVersion7() : null);

        var check = await new SignIn(_store, _clock).CheckAsync("anna", "falsch", Ct);

        Assert.Equal(PasswordCheckStatus.Failed, check.Status);
        var delay = Assert.Single(_clock.Delays);
        Assert.InRange(delay, TimeSpan.FromMilliseconds(1), AccountRules.MinFailedSignInDuration);
    }

    [Fact]
    public async Task SuccessfulAndLockedSignIns_AreNotDelayed()
    {
        _store.CheckResult = PasswordCheck.Success(FakeAccountStore.Someone);
        await new SignIn(_store, _clock).CheckAsync("anna", "richtig", Ct);
        _store.CheckResult = PasswordCheck.Locked(FakeAccountStore.Someone.Id, DateTimeOffset.UtcNow.AddMinutes(5));
        await new SignIn(_store, _clock).CheckAsync("anna", "richtig", Ct);

        Assert.Empty(_clock.Delays);
    }

    [Fact]
    public async Task ChangePassword_PassesTheLockoutOn_AndLogsARemovedAccount()
    {
        var log = new RecordingLogger<ChangePassword>();
        var current = new FakeCurrentAccount();
        var until = DateTimeOffset.UtcNow.AddMinutes(5);

        _store.ChangeResult = AccountResult.Locked<Account>(until);
        var locked = await new ChangePassword(_store, current, log).ChangeAsync("alt", "neues-passwort", Ct);
        _store.ChangeResult = AccountResult.Fail<Account>(AccountStatus.NotFound);
        var gone = await new ChangePassword(_store, current, log).ChangeAsync("alt", "neues-passwort", Ct);

        Assert.Equal((AccountStatus.LockedOut, until), (locked.Status, locked.LockedUntil));
        Assert.Equal(AccountStatus.LockedOut, locked.Cast<Guid>().Status);
        Assert.Equal(until, locked.Cast<Guid>().LockedUntil);
        Assert.Equal(AccountStatus.NotFound, gone.Status);
        Assert.Equal([5113], log.EventIds);
    }

    /// <summary>Records the delays asked for and lets them end at once.</summary>
    private sealed class DelayRecordingClock : TimeProvider
    {
        public List<TimeSpan> Delays { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add(dueTime);
            return System.CreateTimer(callback, state, TimeSpan.Zero, period);
        }
    }
}
