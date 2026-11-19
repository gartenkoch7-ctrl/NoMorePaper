using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Inputs;

/// <summary>
/// Client for the owner check of documents whose owner was unknown on arrival: "fits" keeps the document,
/// "hand over" gives it to another member. 409 means somebody answered already; the caller loads again.
/// </summary>
public sealed class OwnerCheckApi(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<OwnerCheckApi> logger)
{
    private readonly InputsHttp _http = new(http, localizer, logger);

    public Task<ApiResult<bool>> KeepAsync(Guid documentId, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Post, $"api/documents/{documentId}/owner-check", null, "owner-check", Answered, null, ct);

    public Task<ApiResult<bool>> HandOverAsync(Guid documentId, Guid accountId, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Post, $"api/documents/{documentId}/hand-over", new HandOverRequestDto(accountId), "hand-over", Answered, null, ct);

    private string? Answered(int status) => status == 409 ? localizer["InpOwnerCheckAnswered"].Value : null;
}
