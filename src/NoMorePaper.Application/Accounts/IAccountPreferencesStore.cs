using NoMorePaper.Domain.Documents;

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
    /// <returns>
    /// <see cref="PreferencesOutcome.Saved"/>; <see cref="PreferencesOutcome.SenderTaken"/> with the address another
    /// account has; or <see cref="PreferencesOutcome.AccountGone"/> when the account was removed meanwhile. Nothing
    /// is changed unless saved.
    /// </returns>
    Task<PreferencesResult> SaveAsync(Guid accountId, AccountPreferences preferences, CancellationToken cancellationToken);

    /// <summary>The account's choice which versions an edited document keeps (ADR-0025).</summary>
    /// <returns>The choice, or <see cref="VersionPolicy.KeepBoth"/> if it never made one.</returns>
    Task<VersionPolicy> GetAfterEditingAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Saves the account's choice which versions an edited document keeps; the other preferences stay.</summary>
    /// <returns><c>false</c>, changing nothing, when the account was removed meanwhile.</returns>
    Task<bool> SetAfterEditingAsync(Guid accountId, VersionPolicy policy, CancellationToken cancellationToken);

    /// <summary>The account a mail sender belongs to; the address is normalized here (<see cref="AccountPreferences.NormalizeAddress"/>).</summary>
    /// <returns>The account, or <c>null</c> for an unknown or invalid address.</returns>
    Task<Guid?> FindOwnerOfSenderAsync(string address, CancellationToken cancellationToken);
}
