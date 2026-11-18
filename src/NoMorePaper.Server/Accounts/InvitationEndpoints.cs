using NoMorePaper.Application.Accounts;
using NoMorePaper.Contracts.Accounts;

namespace NoMorePaper.Server.Accounts;

/// <summary>
/// Invitation links (created by the administrator, used without sign-in) and reset links (used
/// without sign-in). Unknown, used and expired links all answer 404.
/// </summary>
internal static class InvitationEndpoints
{
    public static RouteGroupBuilder MapInvitationEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/invitations", InviteAsync);
        api.MapGet("/invitations/{token}", CheckAsync).AllowAnonymous();
        api.MapPost("/invitations/{token}/accept", AcceptAsync).AllowAnonymous();
        api.MapPost("/password-reset/{token}", ResetAsync).AllowAnonymous();
        return api;
    }

    private static async Task<IResult> InviteAsync(InviteMember invite, HttpRequest request, CancellationToken cancellationToken)
    {
        var result = await invite.InviteAsync(cancellationToken).ConfigureAwait(false);
        return result.Value is { } link
            ? Results.Ok(new LinkDto(AccountLinks.Build(request, AccountLinks.InvitationPage, link.Token), link.ExpiresAt))
            : AccountResponses.Failure(result, AccountResponses.PasswordField);
    }

    private static async Task<IResult> CheckAsync(string token, AcceptInvitation accept, CancellationToken cancellationToken) =>
        await accept.CheckAsync(token, cancellationToken).ConfigureAwait(false) is { } expiresAt
            ? Results.Ok(new InvitationDto(expiresAt))
            : AccountResponses.NotFound();

    // The new member is signed in right away.
    private static async Task<IResult> AcceptAsync(
        string token,
        AcceptInvitationDto body,
        AcceptInvitation accept,
        AccountSessions sessions,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await accept.AcceptAsync(token, body.UserName, body.DisplayName, body.Password, cancellationToken)
            .ConfigureAwait(false);
        if (result.Value is not { } member)
        {
            return AccountResponses.Failure(result, AccountResponses.PasswordField);
        }

        await sessions.SignInAsync(context, member).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> ResetAsync(
        string token, NewPasswordDto body, ResetPassword reset, CancellationToken cancellationToken)
    {
        var result = await reset.ResetAsync(token, body.NewPassword, cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? Results.NoContent() : AccountResponses.Failure(result, AccountResponses.NewPasswordField);
    }
}
