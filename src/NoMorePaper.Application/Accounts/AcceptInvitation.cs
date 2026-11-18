using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Accounts;

/// <summary>Someone with an invitation link becomes a member; the link works once, within its lifetime.</summary>
public sealed class AcceptInvitation(IAccountStore accounts, TimeProvider clock, ILogger<AcceptInvitation> logger)
{
    /// <returns>When the invitation expires, or <c>null</c> if it is unknown, used or expired.</returns>
    public async Task<DateTimeOffset?> CheckAsync(string? token, CancellationToken cancellationToken) =>
        string.IsNullOrEmpty(token)
            ? null
            : await accounts.FindOpenInvitationAsync(SecretHash.Of(token), clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);

    /// <returns>The new member; <see cref="AccountStatus.NotFound"/> for an unknown, used or expired invitation.</returns>
    public async Task<AccountResult<Account>> AcceptAsync(
        string? token, string? userName, string? displayName, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(token))
        {
            return AccountResult.Fail<Account>(AccountStatus.NotFound);
        }

        var input = AccountInput.Check(userName, displayName, password);
        if (input.Value is not { } member)
        {
            return input.Cast<Account>();
        }

        var result = await accounts.AcceptInvitationAsync(SecretHash.Of(token), clock.GetUtcNow(), member, cancellationToken)
            .ConfigureAwait(false);
        if (result.Value is { } created)
        {
            logger.InvitationAccepted(created.Id);
        }

        return result;
    }
}
