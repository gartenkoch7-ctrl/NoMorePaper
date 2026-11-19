namespace NoMorePaper.Application.Accounts;

/// <summary>
/// The preferences of the accounts in the database. A sender address belongs to at most one account;
/// an account's preferences go with the account when it is removed.
/// </summary>
public interface IAccountPreferencesStore
{
    /// <returns>The account's preferences, or <see cref="AccountPreferences.Default"/> if it never saved any.</returns>
    Task<AccountPreferences> GetAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Replaces the account's preferences, sender addresses included, in one transaction.</summary>
    /// <returns><c>null</c> when saved; otherwise a sender address another account has, and nothing was changed.</returns>
    Task<string?> SaveAsync(Guid accountId, AccountPreferences preferences, CancellationToken cancellationToken);

    /// <summary>The account a mail sender belongs to; the address is normalized here (<see cref="AccountPreferences.NormalizeAddress"/>).</summary>
    /// <returns>The account, or <c>null</c> for an unknown or invalid address.</returns>
    Task<Guid?> FindOwnerOfSenderAsync(string address, CancellationToken cancellationToken);
}
