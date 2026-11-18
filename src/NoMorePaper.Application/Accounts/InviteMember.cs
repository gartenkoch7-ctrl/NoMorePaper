using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>The administrator creates a one-time invitation link, valid for <see cref="AccountRules.InvitationLifetime"/>.</summary>
public sealed class InviteMember(
    IAccountStore accounts, ICurrentAccount current, ISecretGenerator secrets, TimeProvider clock, ILogger<InviteMember> logger)
{
    public async Task<AccountResult<IssuedLink>> InviteAsync(CancellationToken cancellationToken)
    {
        if (!current.IsAdmin)
        {
            logger.AdministratorOnly(current.Id, "invite");
            return AccountResult.Fail<IssuedLink>(AccountStatus.Forbidden);
        }

        var token = secrets.NewToken();
        var expiresAt = AccountRules.Expiry(clock.GetUtcNow(), AccountRules.InvitationLifetime);
        await accounts.AddInvitationAsync(SecretHash.Of(token), current.Id, expiresAt, cancellationToken).ConfigureAwait(false);
        logger.InvitationCreated(current.Id, expiresAt);
        return AccountResult.Success<IssuedLink>(new IssuedLink(token, expiresAt));
    }
}
