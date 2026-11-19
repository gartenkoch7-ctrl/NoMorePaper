using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Web.Client.Accounts;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Inputs;

/// <summary>
/// Client for <c>/api/inputs/mail</c> (administrator only): the household's mailbox, never with its password,
/// and the connection test.
/// </summary>
public sealed class MailSettingsApi(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<MailSettingsApi> logger)
{
    /// <summary>The request field the server names for refused settings.</summary>
    public const string SettingsField = "settings";

    /// <summary>The request field the server names for an unencrypted connection (<c>unencrypted</c>).</summary>
    public const string SecurityField = "security";

    /// <summary>The request field the server names when the password has to be entered (<c>required</c>).</summary>
    public const string PasswordField = "password";

    private readonly InputsHttp _http = new(http, localizer, logger);

    public Task<ApiResult<MailSettingsDto>> GetAsync(CancellationToken ct = default)
        => _http.SendAsync<MailSettingsDto>(HttpMethod.Get, "api/inputs/mail", null, "mail-settings", null, null, ct);

    /// <summary>Saves the set-up; a <c>null</c> or empty password keeps the stored one (same server and user only).</summary>
    public Task<ApiResult<bool>> SaveAsync(MailSettingsRequestDto settings, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Put, "api/inputs/mail", settings, "save-mail-settings", Refused, SettingsField, Named, ct);

    /// <summary>Tries the given set-up; success means the server accepted the sign-in.</summary>
    public Task<ApiResult<bool>> TestAsync(MailSettingsRequestDto settings, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Post, "api/inputs/mail/test", settings, "test-mail-settings", Tested, SettingsField, Named, ct);

    private (string Text, string Field)? Named(RefusedInput refused) => (refused.Field, refused.Word) switch
    {
        (PasswordField, "required") => (localizer["MailErrorPasswordAgain"].Value, PasswordField),
        (SecurityField, "unencrypted") => (localizer["MailErrorUnencrypted"].Value, SecurityField),
        _ => null,
    };

    private string? Refused(int status) => status == 400 ? localizer["MailErrorInvalid"].Value : null;

    private string? Tested(int status) => status switch
    {
        400 => localizer["MailErrorInvalid"].Value,
        422 => localizer["MailTestSignInFailed"].Value,
        502 => localizer["MailTestUnreachable"].Value,
        _ => null,
    };
}
