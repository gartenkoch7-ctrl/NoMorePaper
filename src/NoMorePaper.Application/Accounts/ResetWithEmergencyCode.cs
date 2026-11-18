using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>
/// The administrator, who has nobody to ask for a reset link, sets a new password with the emergency
/// code from the setup. The code works once: a new one is issued and shown instead.
/// </summary>
public sealed class ResetWithEmergencyCode(IAccountStore accounts, ISecretGenerator secrets, ILogger<ResetWithEmergencyCode> logger)
{
    /// <returns>The new emergency code in clear text; <see cref="AccountStatus.Unauthorized"/> for an unknown name or wrong code.</returns>
    public async Task<AccountResult<string>> ResetAsync(
        string? userName, string? emergencyCode, string? newPassword, CancellationToken cancellationToken)
    {
        var code = SecretHash.NormalizeEmergencyCode(emergencyCode);
        var name = AccountInput.UserName(userName);
        if (code.Length == 0 || name.Length == 0)
        {
            logger.EmergencyResetRefused();
            return AccountResult.Fail<string>(AccountStatus.Unauthorized);
        }

        var newCode = secrets.NewEmergencyCode();
        var result = await accounts.ResetWithEmergencyCodeAsync(
            name, SecretHash.Of(code), newPassword ?? string.Empty, SecretHash.Of(newCode), cancellationToken).ConfigureAwait(false);
        if (result.Status == AccountStatus.Unauthorized)
        {
            logger.EmergencyResetRefused();
        }

        if (!result.Succeeded)
        {
            return result.Cast<string>();
        }

        logger.EmergencyReset(result.Value);
        return AccountResult.Success<string>(newCode);
    }
}
