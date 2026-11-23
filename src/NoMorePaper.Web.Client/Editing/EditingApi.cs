using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Editing;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Inputs;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Editing;

/// <summary>
/// Client for editing a document: its pages, a new page order (<c>edit</c>), appending, splitting,
/// the versions to keep, the account's default for that, and the answer to a split suggestion.
/// A started edit answers 202; 409 means an edit runs or the document changed meanwhile.
/// </summary>
public sealed class EditingApi(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<EditingApi> logger)
{
    private readonly InputsHttp _http = new(http, localizer, logger);

    public Task<ApiResult<DocumentPagesDto>> GetPagesAsync(Guid id, CancellationToken ct = default)
        => _http.SendAsync<DocumentPagesDto>(HttpMethod.Get, $"api/documents/{id}/pages", null, "pages", null, null, ct);

    /// <summary>
    /// Sends the new page order. <paramref name="pages"/> carry the JPEG of every step with
    /// <see cref="PageStepDto.Replaced"/> (cropped or straightened in the browser), by its source page.
    /// </summary>
    public Task<ApiResult<bool>> SaveEditAsync(
        Guid id, PagePlanDto plan, IReadOnlyList<(int Page, byte[] Jpeg)> pages, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(pages);
        var form = new MultipartFormDataContent { { JsonContent.Create(plan), "plan" } };
        foreach (var (page, jpeg) in pages)
        {
            var name = string.Create(CultureInfo.InvariantCulture, $"page-{page}");
            var part = new ByteArrayContent(jpeg);
            part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(part, name, name + ".jpg");
        }

        return _http.SendContentAsync<bool>(HttpMethod.Post, $"api/documents/{id}/edit", form, "edit", Conflict, null, null, ct);
    }

    /// <summary>Appends the pages of <paramref name="appendedId"/>; that document goes to the trash afterwards.</summary>
    public Task<ApiResult<bool>> AppendAsync(Guid id, Guid appendedId, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Post, $"api/documents/{id}/append", new AppendRequestDto(appendedId), "append", Conflict, null, ct);

    public Task<ApiResult<bool>> SplitAsync(Guid id, SplitPlanDto plan, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Post, $"api/documents/{id}/split", plan, "split", Conflict, null, ct);

    public Task<ApiResult<bool>> ChooseVersionsAsync(Guid id, VersionPolicyDto policy, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Put, $"api/documents/{id}/versions", new VersionChoiceDto(policy), "versions", Conflict, null, ct);

    public Task<ApiResult<AfterEditingDto>> GetAfterEditingAsync(CancellationToken ct = default)
        => _http.SendAsync<AfterEditingDto>(HttpMethod.Get, "api/account/after-editing", null, "after-editing", null, null, ct);

    public Task<ApiResult<bool>> SetAfterEditingAsync(VersionPolicyDto policy, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Put, "api/account/after-editing", new AfterEditingDto(policy), "set-after-editing", null, null, ct);

    /// <summary>Accepts the suggestion; the document is split as suggested. 409: answered already or an edit is running.</summary>
    public Task<ApiResult<bool>> AcceptSplitAsync(Guid id, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Post, $"api/documents/{id}/split-suggestion/accept", null, "accept-split", Conflict, null, ct);

    public Task<ApiResult<bool>> DismissSplitAsync(Guid id, CancellationToken ct = default)
        => _http.SendAsync<bool>(
            HttpMethod.Post, $"api/documents/{id}/split-suggestion/dismiss", null, "dismiss-split", SplitAnswered, null, ct);

    private string? Conflict(int status) => status == 409 ? localizer["PeErrorConflict"].Value : null;

    private string? SplitAnswered(int status) => status == 409 ? localizer["SplitErrorAnswered"].Value : null;
}
