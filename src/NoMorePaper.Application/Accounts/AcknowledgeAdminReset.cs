using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>
/// The signed-in account has seen the notice that the administrator's reset link changed its
/// password (<see cref="AdminResetNotice"/>). Every unacknowledged reset is marked as seen; the
/// records stay, so a later notice lists them again.
/// </summary>
public sealed class AcknowledgeAdminReset(
    IAccountStore accounts, ICurrentAccount current, TimeProvider clock, ILogger<AcknowledgeAdminReset> logger)
{
    /// <returns><c>false</c> if the account was removed meanwhile.</returns>
    public async Task<bool> AcknowledgeAsync(CancellationToken cancellationToken)
    {
        var acknowledged = await accounts.AcknowledgeAdminResetsAsync(current.Id, clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        if (acknowledged)
        {
            logger.AdminResetAcknowledged(current.Id);
        }
        else
        {
            logger.AccountGone(current.Id, "acknowledge-reset");
        }

        return acknowledged;
    }
}
