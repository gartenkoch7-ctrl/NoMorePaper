using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>All accounts of the household, for the administrator.</summary>
public sealed class ListMembers(IAccountStore accounts, ICurrentAccount current, ILogger<ListMembers> logger)
{
    public async Task<AccountResult<IReadOnlyList<Account>>> ListAsync(CancellationToken cancellationToken)
    {
        if (!current.IsAdmin)
        {
            logger.AdministratorOnly(current.Id, "list-members");
            return AccountResult.Fail<IReadOnlyList<Account>>(AccountStatus.Forbidden);
        }

        var members = await accounts.ListAsync(cancellationToken).ConfigureAwait(false);
        return AccountResult.Success<IReadOnlyList<Account>>(members);
    }
}
