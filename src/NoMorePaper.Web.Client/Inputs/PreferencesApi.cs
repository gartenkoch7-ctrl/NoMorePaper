using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Inputs;

/// <summary>
/// Client for <c>/api/account/preferences</c>: whether new documents are private, and the account's mail
/// senders. Camera, upload and the account page use it.
/// </summary>
public sealed class PreferencesApi(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<PreferencesApi> logger)
{
    /// <summary>The request field the server names for a refused address.</summary>
    public const string SendersField = "mailSenders";

    private readonly InputsHttp _http = new(http, localizer, logger);

    public Task<ApiResult<AccountPreferencesDto>> GetAsync(CancellationToken ct = default)
        => _http.SendAsync<AccountPreferencesDto>(HttpMethod.Get, "api/account/preferences", null, "preferences", null, null, ct);

    /// <summary>
    /// Saves the preferences. A refused address comes back with <see cref="ApiResult{T}.Field"/> =
    /// <see cref="SendersField"/>: invalid (400) or another account has it (409).
    /// </summary>
    public Task<ApiResult<bool>> SaveAsync(AccountPreferencesDto preferences, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Put, "api/account/preferences", preferences, "save-preferences", RefusedSender, SendersField, ct);

    private string? RefusedSender(int status) => status switch
    {
        400 => localizer["InpErrorSenderInvalid"].Value,
        409 => localizer["InpErrorSenderTaken"].Value,
        _ => null,
    };
}
