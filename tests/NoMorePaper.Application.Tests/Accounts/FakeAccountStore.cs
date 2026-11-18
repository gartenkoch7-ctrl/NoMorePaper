using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Tests.Accounts;

/// <summary>Records what the use cases hand to the store; the answers are set by the test.</summary>
internal sealed class FakeAccountStore : IAccountStore
{
    public static readonly Account Someone = new(Guid.CreateVersion7(), "anna", "Anna", false, "stamp");

    public bool HasAccounts { get; set; }

    public List<string> Calls { get; } = [];

    /// <summary>Hashes the store received, in call order.</summary>
    public List<string> Hashes { get; } = [];

    public List<DateTimeOffset> Expiries { get; } = [];

    public AccountResult<HouseholdSetUp> SetUpResult { get; set; } = AccountResult.Success(new HouseholdSetUp(Someone, 2));

    public bool KnownAccount { get; set; } = true;

    public AccountResult<Account> AcceptResult { get; set; } = AccountResult.Success(Someone);

    public AccountResult<Guid> ResetResult { get; set; } = AccountResult.Success(Someone.Id);

    public Task<bool> AnyAccountAsync(CancellationToken cancellationToken) => Task.FromResult(HasAccounts);

    public Task<AccountResult<HouseholdSetUp>> SetUpAsync(NewAccount administrator, string emergencyCodeHash, CancellationToken cancellationToken)
    {
        Record(nameof(SetUpAsync), emergencyCodeHash);
        return Task.FromResult(SetUpResult);
    }

    public Task<PasswordCheck> CheckPasswordAsync(string userName, string password, CancellationToken cancellationToken)
    {
        Calls.Add($"{nameof(CheckPasswordAsync)}:{userName}");
        return Task.FromResult(PasswordCheck.Failure(null));
    }

    public Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Account?>(KnownAccount ? Someone : null);

    public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ListAsync));
        return Task.FromResult<IReadOnlyList<Account>>([Someone]);
    }

    public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(RemoveAsync));
        return Task.FromResult(KnownAccount);
    }

    public Task<bool> RenewSecurityStampAsync(Guid id, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(RenewSecurityStampAsync));
        return Task.FromResult(KnownAccount);
    }

    public Task<AccountResult<Account>> ChangePasswordAsync(Guid id, string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        Calls.Add(nameof(ChangePasswordAsync));
        return Task.FromResult(AccountResult.Success(Someone));
    }

    public Task AddInvitationAsync(string tokenHash, Guid createdBy, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        Record(nameof(AddInvitationAsync), tokenHash, expiresAt);
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> FindOpenInvitationAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Record(nameof(FindOpenInvitationAsync), tokenHash);
        return Task.FromResult<DateTimeOffset?>(null);
    }

    public Task<AccountResult<Account>> AcceptInvitationAsync(string tokenHash, DateTimeOffset now, NewAccount member, CancellationToken cancellationToken)
    {
        Record(nameof(AcceptInvitationAsync), tokenHash);
        return Task.FromResult(AcceptResult);
    }

    public Task<bool> AddResetTokenAsync(Guid accountId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        Record(nameof(AddResetTokenAsync), tokenHash, expiresAt);
        return Task.FromResult(KnownAccount);
    }

    public Task<AccountResult<Guid>> ResetPasswordAsync(string tokenHash, DateTimeOffset now, string newPassword, CancellationToken cancellationToken)
    {
        Record(nameof(ResetPasswordAsync), tokenHash);
        return Task.FromResult(ResetResult);
    }

    public Task<AccountResult<Guid>> ResetWithEmergencyCodeAsync(
        string userName, string codeHash, string newPassword, string newCodeHash, CancellationToken cancellationToken)
    {
        Record($"{nameof(ResetWithEmergencyCodeAsync)}:{userName}", codeHash);
        Hashes.Add(newCodeHash);
        return Task.FromResult(ResetResult);
    }

    private void Record(string call, string hash, DateTimeOffset? expiresAt = null)
    {
        Calls.Add(call);
        Hashes.Add(hash);
        if (expiresAt is { } expiry)
        {
            Expiries.Add(expiry);
        }
    }
}
