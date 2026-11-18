using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>Ends every session of the signed-in account, on all devices, by renewing its security stamp.</summary>
public sealed class SignOutEverywhere(IAccountStore accounts, ICurrentAccount current, ILogger<SignOutEverywhere> logger)
{
    /// <returns><c>false</c> if the account was removed meanwhile.</returns>
    public async Task<bool> SignOutAsync(CancellationToken cancellationToken)
    {
        var renewed = await accounts.RenewSecurityStampAsync(current.Id, cancellationToken).ConfigureAwait(false);
        if (renewed)
        {
            logger.SignedOutEverywhere(current.Id);
        }

        return renewed;
    }
}
