namespace NoMorePaper.Application.Accounts;

/// <summary>
/// The members of the household for every signed-in account, e.g. to choose the editors of a document.
/// Callers show only id and display name; user names stay with the administrator (<see cref="ListMembers"/>).
/// </summary>
public sealed class ListHousehold(IAccountStore accounts)
{
    /// <returns>All accounts, ordered by display name.</returns>
    public Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken) => accounts.ListAsync(cancellationToken);
}
