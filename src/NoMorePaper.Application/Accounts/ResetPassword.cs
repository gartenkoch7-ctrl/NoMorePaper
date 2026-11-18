using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>A member sets a new password with a reset link from the administrator.</summary>
public sealed class ResetPassword(IAccountStore accounts, TimeProvider clock, ILogger<ResetPassword> logger)
{
    /// <returns><see cref="AccountStatus.NotFound"/> for an unknown, used or expired link.</returns>
    public async Task<AccountResult> ResetAsync(string? token, string? newPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return AccountResult.Fail(AccountStatus.NotFound);
        }

        var result = await accounts.ResetPasswordAsync(SecretHash.Of(token), clock.GetUtcNow(), newPassword ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);
        if (result.Succeeded)
        {
            logger.PasswordReset(result.Value);
        }

        return result;
    }
}
