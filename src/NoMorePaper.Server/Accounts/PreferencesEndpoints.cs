using NoMorePaper.Application.Accounts;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Server.Inputs;

namespace NoMorePaper.Server.Accounts;

/// <summary>
/// <c>GET/PUT /api/account/preferences</c>: the signed-in account's choice for new documents and its mail
/// senders. A refused address is a validation problem on <c>mailSenders</c>: 400 <c>invalid</c>, or 409
/// <c>taken</c> when another account has it.
/// </summary>
internal static class PreferencesEndpoints
{
    public const string SendersField = "mailSenders";

    public static RouteGroupBuilder MapPreferencesEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/account/preferences", GetAsync);
        api.MapPut("/account/preferences", ChangeAsync);
        return api;
    }

    private static async Task<IResult> GetAsync(GetPreferences get, CancellationToken cancellationToken) =>
        Results.Ok((await get.GetAsync(cancellationToken).ConfigureAwait(false)).ToDto());

    private static async Task<IResult> ChangeAsync(
        AccountPreferencesDto? body,
        ChangePreferences change,
        ICurrentAccount current,
        AccountSessions sessions,
        HttpContext context,
        ILogger<ChangePreferences> logger,
        CancellationToken cancellationToken)
    {
        if (body is null)
        {
            logger.PreferencesRefused(current.Id, "empty");
            return Refused("invalid", StatusCodes.Status400BadRequest);
        }

        var result = await change.ChangeAsync(body.NewDocumentsPrivate, body.MailSenders, cancellationToken).ConfigureAwait(false);
        switch (result.Outcome)
        {
            case PreferencesOutcome.Saved:
                logger.PreferencesSaved(current.Id, body.MailSenders?.Count ?? 0);
                return Results.NoContent();
            case PreferencesOutcome.AccountGone:
                return await sessions.EndRemovedAsync(context).ConfigureAwait(false);
            case PreferencesOutcome.SenderTaken:
                logger.PreferencesRefused(current.Id, "sender-taken");
                return Refused("taken", StatusCodes.Status409Conflict);
            default:
                logger.PreferencesRefused(current.Id, "invalid");
                return Refused("invalid", StatusCodes.Status400BadRequest);
        }
    }

    private static IResult Refused(string word, int status) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [SendersField] = [word] }, statusCode: status);
}
