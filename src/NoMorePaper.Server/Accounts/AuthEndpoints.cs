using NoMorePaper.Application.Accounts;
using NoMorePaper.Contracts.Accounts;

namespace NoMorePaper.Server.Accounts;

/// <summary>Sign-in, sign-out, "who am I" and password change, below the <c>/api</c> group.</summary>
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
        GetAccount get, ICurrentAccount current, AccountSessions sessions, HttpContext context, CancellationToken cancellationToken)
    {
        var account = await get.FindAsync(current.Id, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            await sessions.SignOutAsync(context).ConfigureAwait(false);
            return AccountResponses.SignInRequired();
        }

        return Results.Ok(account.ToDto());
    }

    // The security stamp changed with the password: this device gets a fresh cookie, all others end.
    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordDto body, ChangePassword change, AccountSessions sessions, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await change.ChangeAsync(body.CurrentPassword, body.NewPassword, cancellationToken).ConfigureAwait(false);
        if (result.Value is not { } account)
        {
            return AccountResponses.Failure(result, AccountResponses.NewPasswordField);
        }

        await sessions.SignInAsync(context, account).ConfigureAwait(false);
        return Results.NoContent();
    }
}
