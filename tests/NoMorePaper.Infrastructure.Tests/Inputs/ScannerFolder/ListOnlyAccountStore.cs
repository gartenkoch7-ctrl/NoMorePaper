using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Infrastructure.Tests.Inputs.ScannerFolder;

/// <summary>An account store that only lists the given accounts: all the scanner subfolders need.</summary>
internal sealed class ListOnlyAccountStore(params Account[] accounts) : IAccountStore
{
    public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Account>>(accounts);

    public Task<bool> AnyAccountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<AccountResult<HouseholdSetUp>> SetUpAsync(NewAccount administrator, string emergencyCodeHash, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<PasswordCheck> CheckPasswordAsync(string userName, string password, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<Guid?> FindAdministratorIdAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> RemoveAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> RenewSecurityStampAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<AccountResult<Account>> ChangePasswordAsync(Guid id, string currentPassword, string newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task AddInvitationAsync(string tokenHash, Guid createdBy, DateTimeOffset expiresAt, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<DateTimeOffset?> FindOpenInvitationAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<AccountResult<Account>> AcceptInvitationAsync(string tokenHash, DateTimeOffset now, NewAccount member, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> AddResetTokenAsync(Guid accountId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<AccountResult<Guid>> ResetPasswordAsync(string tokenHash, DateTimeOffset now, string newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<IReadOnlyList<AdminReset>> ListAdminResetsAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<bool> AcknowledgeAdminResetsAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<AccountResult<Guid>> ResetWithEmergencyCodeAsync(
        string userName, string codeHash, string newPassword, string newCodeHash, CancellationToken cancellationToken) => throw new NotSupportedException();
}
