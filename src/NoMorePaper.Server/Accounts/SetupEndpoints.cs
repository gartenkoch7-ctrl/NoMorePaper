using NoMorePaper.Application.Accounts;
using NoMorePaper.Contracts.Accounts;

namespace NoMorePaper.Server.Accounts;

/// <summary>First start and the administrator's emergency reset; both work without sign-in.</summary>
internal static class SetupEndpoints
{
    public static RouteGroupBuilder MapSetupEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/setup", StatusAsync).AllowAnonymous();
        api.MapPost("/setup", SetUpAsync).AllowAnonymous();
        api.MapPost("/emergency-reset", EmergencyResetAsync).AllowAnonymous();
        return api;
    }

    private static async Task<IResult> StatusAsync(SetUpHousehold setUp, CancellationToken cancellationToken) =>
        Results.Ok(new SetupStatusDto(await setUp.IsNeededAsync(cancellationToken).ConfigureAwait(false)));

    // The administrator is signed in right away; the code is in this answer and nowhere else.
    private static async Task<IResult> SetUpAsync(
        SetupDto body, SetUpHousehold setUp, AccountSessions sessions, HttpContext context, CancellationToken cancellationToken)
    {
        var result = await setUp.SetUpAsync(body.UserName, body.DisplayName, body.Password, cancellationToken).ConfigureAwait(false);
        if (result.Value is not { } completed)
        {
            return AccountResponses.Failure(result, AccountResponses.PasswordField);
        }

        await sessions.SignInAsync(context, completed.Administrator).ConfigureAwait(false);
        return Results.Ok(new EmergencyCodeDto(completed.EmergencyCode));
    }

    private static async Task<IResult> EmergencyResetAsync(
        EmergencyResetDto body, ResetWithEmergencyCode reset, CancellationToken cancellationToken)
    {
        var result = await reset.ResetAsync(body.UserName, body.EmergencyCode, body.NewPassword, cancellationToken).ConfigureAwait(false);
        return result.Value is { } newCode
            ? Results.Ok(new EmergencyCodeDto(newCode))
            : AccountResponses.Failure(result, AccountResponses.NewPasswordField);
    }
}
