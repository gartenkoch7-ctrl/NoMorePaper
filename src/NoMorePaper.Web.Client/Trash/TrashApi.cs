using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Trash;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Inputs;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Trash;

/// <summary>
/// Client for the trash of the signed-in account and for putting a document into it. 409: the entry
/// cannot be restored now (its document lies in the trash itself) or the document cannot be moved.
/// </summary>
public sealed class TrashApi(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<TrashApi> logger)
{
    private readonly InputsHttp _http = new(http, localizer, logger);

    public Task<ApiResult<TrashItemDto[]>> ListAsync(CancellationToken ct = default)
        => _http.SendAsync<TrashItemDto[]>(HttpMethod.Get, "api/trash", null, "trash", null, null, ct);

    public Task<ApiResult<bool>> RestoreAsync(Guid id, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Post, $"api/trash/{id}/restore", null, "restore", Conflict, null, ct);

    public Task<ApiResult<bool>> PurgeAsync(Guid id, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Delete, $"api/trash/{id}", null, "purge", Conflict, null, ct);

    public Task<ApiResult<bool>> EmptyAsync(CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Delete, "api/trash", null, "empty-trash", null, null, ct);

    public Task<ApiResult<bool>> MoveDocumentAsync(Guid documentId, CancellationToken ct = default)
        => _http.SendAsync<bool>(HttpMethod.Post, $"api/documents/{documentId}/trash", null, "move-to-trash", MoveConflict, null, ct);

    private string? Conflict(int status) => status == 409 ? localizer["TrashErrorConflict"].Value : null;

    // In the viewer the trash list is not open; the document itself changed or is being edited.
    private string? MoveConflict(int status) => status == 409 ? localizer["PeErrorConflict"].Value : null;
}
