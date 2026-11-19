using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Infrastructure.Tests.Accounts;

/// <summary>
/// An account store for intake tests that do not create accounts: every id exists, and
/// <see cref="Administrator"/> takes documents of unknown owners. Everything else is not needed.
/// </summary>
internal sealed class EveryAccountExists(Guid? administrator = null) : IAccountStore
{
    public Guid? Administrator { get; } = administrator ?? Guid.CreateVersion7();

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(true);

    public Task<Guid?> FindAdministratorIdAsync(CancellationToken cancellationToken) => Task.FromResult(Administrator);

    public Task<bool> AnyAccountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<AccountResult<HouseholdSetUp>> SetUpAsync(NewAccount administrator, string emergencyCodeHash, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<PasswordCheck> CheckPasswordAsync(string userName, string password, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> RenewSecurityStampAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<AccountResult<Account>> ChangePasswordAsync(Guid id, string currentPassword, string newPassword, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task AddInvitationAsync(string tokenHash, Guid createdBy, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<DateTimeOffset?> FindOpenInvitationAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<AccountResult<Account>> AcceptInvitationAsync(string tokenHash, DateTimeOffset now, NewAccount member, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<bool> AddResetTokenAsync(Guid accountId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<AccountResult<Guid>> ResetPasswordAsync(string tokenHash, DateTimeOffset now, string newPassword, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<AdminReset>> ListAdminResetsAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> AcknowledgeAdminResetsAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<AccountResult<Guid>> ResetWithEmergencyCodeAsync(
        string userName, string codeHash, string newPassword, string newCodeHash, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
