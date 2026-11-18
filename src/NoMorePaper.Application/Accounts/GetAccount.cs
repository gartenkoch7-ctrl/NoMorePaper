namespace NoMorePaper.Application.Accounts;

/// <summary>An account by id: for "who am I" and for the periodic check that a session is still valid.</summary>
public sealed class GetAccount(IAccountStore accounts)
{
    /// <returns>The account, or <c>null</c> if it was removed.</returns>
    public Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken) => accounts.FindAsync(id, cancellationToken);
}
