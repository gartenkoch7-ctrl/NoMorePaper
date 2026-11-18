using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>The signed-in account changes its own password; all its other sessions end.</summary>
public sealed class ChangePassword(IAccountStore accounts, ICurrentAccount current, ILogger<ChangePassword> logger)
{
    /// <returns>
    /// The account with its new security stamp, so the host can renew the current session;
    /// <see cref="AccountStatus.LockedOut"/> while locked; <see cref="AccountStatus.NotFound"/> if the
    /// account was removed.
    /// </returns>
    public async Task<AccountResult<Account>> ChangeAsync(string? currentPassword, string? newPassword, CancellationToken cancellationToken)
    {
        var result = await accounts.ChangePasswordAsync(
            current.Id, currentPassword ?? string.Empty, newPassword ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded)
        {
            logger.PasswordChanged(current.Id);
        }
        else if (result.Status == AccountStatus.NotFound)
        {
            logger.AccountGone(current.Id, "change-password");
        }

        return result;
    }
}
