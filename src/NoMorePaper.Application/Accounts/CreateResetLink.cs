using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>
/// The administrator creates a one-time link with which a member who forgot the password sets a new
/// one, valid for <see cref="AccountRules.ResetLinkLifetime"/>.
/// </summary>
public sealed class CreateResetLink(
    IAccountStore accounts, ICurrentAccount current, ISecretGenerator secrets, TimeProvider clock, ILogger<CreateResetLink> logger)
{
    public async Task<AccountResult<IssuedLink>> CreateAsync(Guid memberId, CancellationToken cancellationToken)
    {
        if (!current.IsAdmin)
        {
            logger.AdministratorOnly(current.Id, "reset-link");
            return AccountResult.Fail<IssuedLink>(AccountStatus.Forbidden);
        }

        var token = secrets.NewToken();
        var expiresAt = AccountRules.Expiry(clock.GetUtcNow(), AccountRules.ResetLinkLifetime);
        if (!await accounts.AddResetTokenAsync(memberId, SecretHash.Of(token), expiresAt, cancellationToken).ConfigureAwait(false))
        {
            return AccountResult.Fail<IssuedLink>(AccountStatus.NotFound);
        }

        logger.ResetLinkCreated(current.Id, memberId, expiresAt);
        return AccountResult.Success<IssuedLink>(new IssuedLink(token, expiresAt));
    }
}
