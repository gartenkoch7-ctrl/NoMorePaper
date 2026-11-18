using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>
/// The signed-in account has seen the notice that the administrator's reset link changed its
/// password (<see cref="Account.AdminResetAt"/>); the notice is not shown again.
/// </summary>
public sealed class AcknowledgeAdminReset(IAccountStore accounts, ICurrentAccount current, ILogger<AcknowledgeAdminReset> logger)
{
    /// <returns><c>false</c> if the account was removed meanwhile.</returns>
    public async Task<bool> AcknowledgeAsync(CancellationToken cancellationToken)
    {
        var acknowledged = await accounts.ClearAdminResetAsync(current.Id, cancellationToken).ConfigureAwait(false);
        if (acknowledged)
        {
            logger.AdminResetAcknowledged(current.Id);
        }

        return acknowledged;
    }
}
