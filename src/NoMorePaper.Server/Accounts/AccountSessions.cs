using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NoMorePaper.Application.Accounts;
using NoMorePaper.Server.Telemetry;

namespace NoMorePaper.Server.Accounts;

/// <summary>
/// Issues and removes the sign-in cookie and answers a password check (204, 401, 423 with
/// <c>Retry-After</c>) or a password change of a locked account.
/// </summary>
internal sealed class AccountSessions(TimeProvider clock, ILogger<AccountSessions> logger)
{
    private static readonly Counter<long> s_signIns = ServerTelemetry.Meter.CreateCounter<long>(
        "nomorepaper.auth.sign_ins", description: "Sign-in attempts, by outcome");

    /// <summary>A persistent cookie: the session survives closing the browser (30 days, sliding).</summary>
    public async Task SignInAsync(HttpContext context, Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            AccountClaims.CreatePrincipal(account),
            new AuthenticationProperties { IsPersistent = true }).ConfigureAwait(false);
        logger.SignedIn(account.Id);
    }

    public async Task SignOutAsync(HttpContext context)
    {
        if (AccountClaims.AccountId(context.User) is { } id)
        {
            logger.SignedOut(id);
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).ConfigureAwait(false);
    }

    /// <summary>
    /// The answer when the signed-in account was removed but its cookie is not checked yet: the cookie
    /// is cleared and the client is asked to sign in (401).
    /// </summary>
    public async Task<IResult> EndRemovedAsync(HttpContext context)
    {
        await SignOutAsync(context).ConfigureAwait(false);
        return AccountResponses.SignInRequired();
    }

    public async Task<IResult> AnswerAsync(HttpContext context, PasswordCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        switch (check.Status)
        {
            case PasswordCheckStatus.Succeeded when check.Account is { } account:
                await SignInAsync(context, account).ConfigureAwait(false);
                Count("succeeded");
                return Results.NoContent();

            case PasswordCheckStatus.LockedOut when check.LockedUntil is { } until && check.AccountId is { } lockedId:
                var seconds = SetRetryAfter(context, until);
                Count("locked");
                logger.SignInLocked(lockedId, seconds);
                return AccountResponses.Locked();

            default:
                Count("failed");
                if (check.AccountId is { } failedId)
                {
                    logger.SignInFailed(failedId);
                }
                else
                {
                    logger.SignInUnknown();
                }

                // The same answer for unknown names and wrong passwords.
                return AccountResponses.Problem(StatusCodes.Status401Unauthorized, "Wrong user name or password.");
        }
    }

    /// <summary>The answer to a password change while the account is locked: 423 with <c>Retry-After</c>, like at sign-in.</summary>
    public IResult PasswordChangeLocked(HttpContext context, Guid accountId, DateTimeOffset until)
    {
        logger.PasswordChangeLocked(accountId, SetRetryAfter(context, until));
        return AccountResponses.Locked();
    }

    // Whole seconds, rounded up, so a client that waits that long is not refused again.
    private long SetRetryAfter(HttpContext context, DateTimeOffset until)
    {
        var seconds = Math.Max(1, (long)Math.Ceiling((until - clock.GetUtcNow()).TotalSeconds));
        context.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return seconds;
    }

    private static void Count(string outcome) =>
        s_signIns.Add(1, new KeyValuePair<string, object?>("nomorepaper.auth.outcome", outcome));
}
