using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Inputs;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Inputs;

/// <summary>
/// Client for <c>GET /api/inputs</c>: the state of every input the account may see, for the page
/// "Inputs". Mail and iCloud set-up have clients of their own.
/// </summary>
public sealed class InputsApi(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<InputsApi> logger)
{
    private readonly InputsHttp _http = new(http, localizer, logger);

    public Task<ApiResult<InputStatusDto[]>> ListAsync(CancellationToken ct = default)
        => _http.SendAsync<InputStatusDto[]>(HttpMethod.Get, "api/inputs", null, "inputs", null, null, ct);
}
