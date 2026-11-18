using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Server.Accounts;

/// <summary>
/// Checks a sign-in cookie against the store every <see cref="AccountSessionOptions.ValidationInterval"/>,
/// like Identity's security stamp validator: a removed account or a renewed security stamp (password
/// changed, signed out everywhere) ends the session; otherwise the cookie is renewed with fresh claims.
/// </summary>
internal sealed class SessionValidator(
    GetAccount accounts, TimeProvider clock, IOptions<AccountSessionOptions> options, ILogger<SessionValidator> logger)
{
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var now = clock.GetUtcNow();
        if (context.Properties.IssuedUtc is { } issued && now - issued < options.Value.ValidationInterval)
        {
            return;
        }

        var id = AccountClaims.AccountId(context.Principal);
        var account = id is { } accountId
            ? await accounts.FindAsync(accountId, context.HttpContext.RequestAborted).ConfigureAwait(false)
            : null;
        if (account is null || context.Principal is null
            || !SecretHash.AreEqual(account.SecurityStamp, AccountClaims.Stamp(context.Principal)))
        {
            logger.SessionEnded(id ?? Guid.Empty);
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
            return;
        }

        // Fresh claims (administrator flag) and a new issue time, so the next check is due in one interval.
        context.ReplacePrincipal(AccountClaims.CreatePrincipal(account));
        context.ShouldRenew = true;
    }
}
