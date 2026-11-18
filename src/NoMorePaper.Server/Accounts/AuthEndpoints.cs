using NoMorePaper.Application.Accounts;
using NoMorePaper.Contracts.Accounts;

namespace NoMorePaper.Server.Accounts;

/// <summary>Sign-in, sign-out, "who am I", password change and the reset notice, below the <c>/api</c> group.</summary>
internal static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/auth/login", SignInAsync).AllowAnonymous();

        // Anonymous, so a client whose session already ended can still clear its cookie.
        api.MapPost("/auth/logout", SignOutAsync).AllowAnonymous();
        api.MapPost("/auth/logout-everywhere", SignOutEverywhereAsync);
        api.MapGet("/auth/me", MeAsync);
        api.MapPost("/account/password", ChangePasswordAsync);
        api.MapPost("/account/acknowledge-reset", AcknowledgeResetAsync);
        return api;
    }

    private static async Task<IResult> SignInAsync(
        SignInDto body, SignIn signIn, AccountSessions sessions, HttpContext context, CancellationToken cancellationToken)
    {
        var check = await signIn.CheckAsync(body.UserName, body.Password, cancellationToken).ConfigureAwait(false);
        return await sessions.AnswerAsync(context, check).ConfigureAwait(false);
    }

    private static async Task<IResult> SignOutAsync(AccountSessions sessions, HttpContext context)
    {
        await sessions.SignOutAsync(context).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> SignOutEverywhereAsync(
        SignOutEverywhere signOut, AccountSessions sessions, HttpContext context, CancellationToken cancellationToken)
    {
        await signOut.SignOutAsync(cancellationToken).ConfigureAwait(false);
        await sessions.SignOutAsync(context).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> MeAsync(
        GetAccount get,
        GetAdminResetNotice notice,
        ICurrentAccount current,
        AccountSessions sessions,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var account = await get.FindAsync(current.Id, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return await sessions.EndRemovedAsync(context).ConfigureAwait(false);
        }

        return Results.Ok(account.ToOwnDto(await notice.GetAsync(cancellationToken).ConfigureAwait(false)));
    }

    // The account saw the notice that the administrator's reset link changed its password.
    private static async Task<IResult> AcknowledgeResetAsync(
        AcknowledgeAdminReset acknowledge, AccountSessions sessions, HttpContext context, CancellationToken cancellationToken) =>
        await acknowledge.AcknowledgeAsync(cancellationToken).ConfigureAwait(false)
            ? Results.NoContent()
            : await sessions.EndRemovedAsync(context).ConfigureAwait(false);

    // The security stamp changed with the password: this device gets a fresh cookie, all others end.
    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordDto body,
        ChangePassword change,
        ICurrentAccount current,
        AccountSessions sessions,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await change.ChangeAsync(body.CurrentPassword, body.NewPassword, cancellationToken).ConfigureAwait(false);
        if (result.Value is not { } account)
        {
            return result.Status switch
            {
                AccountStatus.LockedOut => sessions.PasswordChangeLocked(context, current.Id, result.LockedUntil!.Value),

                // The account was removed; its cookie is not checked yet.
                AccountStatus.NotFound => await sessions.EndRemovedAsync(context).ConfigureAwait(false),
                _ => AccountResponses.Failure(result, AccountResponses.NewPasswordField),
            };
        }

        await sessions.SignInAsync(context, account).ConfigureAwait(false);
        return Results.NoContent();
    }
}
