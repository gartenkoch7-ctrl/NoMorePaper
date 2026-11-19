using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Contracts.Inputs;

namespace NoMorePaper.Server.Inputs;

/// <summary>
/// The household's mailbox, administrator only (ADR-0024): <c>GET /api/inputs/mail</c> (never the password,
/// only <c>hasPassword</c>), <c>PUT /api/inputs/mail</c>, and <c>POST /api/inputs/mail/test</c>, which tries
/// the posted set-up: 204 signed in, 422 sign-in refused, 502 server not reachable. Refused input is 400
/// with <c>errors: { field: ["invalid"|"required"] }</c>; others than the administrator get 403.
/// </summary>
internal static class MailSettingsEndpoints
{
    public const string SettingsField = "settings";
    public const string PasswordField = "password";

    public static RouteGroupBuilder MapMailSettingsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/inputs/mail", GetAsync);
        api.MapPut("/inputs/mail", SaveAsync);
        api.MapPost("/inputs/mail/test", TestAsync);
        return api;
    }

    private static async Task<IResult> GetAsync(ConfigureMailbox configure, CancellationToken cancellationToken)
    {
        if (await configure.GetAsync(cancellationToken).ConfigureAwait(false) is not { } setup)
        {
            return Forbidden();
        }

        // Not set up yet: an empty form with the usual port for TLS.
        var settings = setup.Settings ?? new MailboxSettings(string.Empty, 993, MailboxSecurity.Ssl, string.Empty, null);
        return Results.Ok(new MailSettingsDto(settings.Host, settings.Port, Word(settings.Security), settings.UserName, settings.TrustedAuthServId, setup.HasPassword));
    }

    private static async Task<IResult> SaveAsync(
        MailSettingsRequestDto? body, ConfigureMailbox configure, ICurrentAccount current, ILogger<ConfigureMailbox> logger,
        CancellationToken cancellationToken)
    {
        if (!current.IsAdmin)
        {
            return Failure(MailboxSetupStatus.Forbidden, current, logger);
        }

        if (Settings(body) is not { } settings)
        {
            logger.MailboxRefused(current.Id, "invalid");
            return Refused(SettingsField, "invalid");
        }

        var status = await configure.SaveAsync(settings, body!.Password, cancellationToken).ConfigureAwait(false);
        if (status == MailboxSetupStatus.Done)
        {
            var renewed = !string.IsNullOrEmpty(body.Password);
            logger.MailboxSaved(current.Id, settings.Security, renewed);
            return Results.NoContent();
        }

        return Failure(status, current, logger);
    }

    private static async Task<IResult> TestAsync(
        MailSettingsRequestDto? body, ConfigureMailbox configure, ICurrentAccount current, ILogger<ConfigureMailbox> logger,
        CancellationToken cancellationToken)
    {
        if (!current.IsAdmin)
        {
            return Failure(MailboxSetupStatus.Forbidden, current, logger);
        }

        if (Settings(body) is not { } settings)
        {
            logger.MailboxRefused(current.Id, "invalid");
            return Refused(SettingsField, "invalid");
        }

        var (status, result) = await configure.TestAsync(settings, body!.Password, cancellationToken).ConfigureAwait(false);
        if (result is not { } found)
        {
            return Failure(status, current, logger);
        }

        logger.MailboxTested(current.Id, found);
        return found switch
        {
            MailboxProbeResult.Ok => Results.NoContent(),
            MailboxProbeResult.SignInFailed => Results.Problem(title: "The mail server refused the sign-in.", statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Results.Problem(title: "The mail server is not reachable.", statusCode: StatusCodes.Status502BadGateway),
        };
    }

    private static IResult Failure(MailboxSetupStatus status, ICurrentAccount current, ILogger logger)
    {
        switch (status)
        {
            case MailboxSetupStatus.Forbidden:
                logger.MailboxRefused(current.Id, "not-administrator");
                return Forbidden();
            case MailboxSetupStatus.PasswordMissing:
                logger.MailboxRefused(current.Id, "password-missing");
                return Refused(PasswordField, "required");
            default:
                logger.MailboxRefused(current.Id, "invalid");
                return Refused(SettingsField, "invalid");
        }
    }

    private static MailboxSettings? Settings(MailSettingsRequestDto? body)
    {
        if (body is null || Security(body.Security) is not { } security)
        {
            return null;
        }

        var authServId = string.IsNullOrWhiteSpace(body.TrustedAuthServId) ? null : body.TrustedAuthServId.Trim();
        var settings = new MailboxSettings(body.Host?.Trim() ?? string.Empty, body.Port, security, body.UserName?.Trim() ?? string.Empty, authServId);
        return settings.IsValid ? settings : null;
    }

    private static MailboxSecurity? Security(string? word) => word switch
    {
        "ssl" => MailboxSecurity.Ssl,
        "starttls" => MailboxSecurity.StartTls,
        "none" => MailboxSecurity.None,
        _ => null,
    };

    private static string Word(MailboxSecurity security) => security switch
    {
        MailboxSecurity.StartTls => "starttls",
        MailboxSecurity.None => "none",
        _ => "ssl",
    };

    private static IResult Forbidden() =>
        Results.Problem(title: "Only the administrator may do this.", statusCode: StatusCodes.Status403Forbidden);

    private static IResult Refused(string field, string word) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { [field] = [word] });
}
