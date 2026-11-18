using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>
/// First start: whoever sets up the household becomes its administrator and receives the emergency
/// code once. Only possible while no account exists; documents from before (M1) go to the administrator.
/// </summary>
public sealed class SetUpHousehold(IAccountStore accounts, ISecretGenerator secrets, ILogger<SetUpHousehold> logger)
{
    public async Task<bool> IsNeededAsync(CancellationToken cancellationToken) =>
        !await accounts.AnyAccountAsync(cancellationToken).ConfigureAwait(false);

    /// <returns>The administrator and the emergency code in clear text, which is never shown again.</returns>
    public async Task<AccountResult<SetupCompleted>> SetUpAsync(
        string? userName, string? displayName, string? password, CancellationToken cancellationToken)
    {
        // Answered before the input is checked, so a finished setup always says so.
        if (await accounts.AnyAccountAsync(cancellationToken).ConfigureAwait(false))
        {
            logger.SetupRefused();
            return AccountResult.Fail<SetupCompleted>(AccountStatus.Conflict);
        }

        var input = AccountInput.Check(userName, displayName, password);
        if (input.Value is not { } administrator)
        {
            return input.Cast<SetupCompleted>();
        }

        var code = secrets.NewEmergencyCode();
        var result = await accounts.SetUpAsync(administrator, SecretHash.Of(code), cancellationToken).ConfigureAwait(false);
        if (result.Value is not { } setUp)
        {
            if (result.Status == AccountStatus.Conflict)
            {
                logger.SetupRefused();
            }

            return result.Cast<SetupCompleted>();
        }

        logger.HouseholdSetUp(setUp.Administrator.Id, setUp.AdoptedDocuments);
        return AccountResult.Success<SetupCompleted>(new SetupCompleted(setUp.Administrator, code));
    }
}
