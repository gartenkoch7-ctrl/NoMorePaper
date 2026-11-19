using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Classification;

/// <summary>
/// Client for the classification API: fields of a document, the list "to review" and the maintained
/// lists. Like <see cref="DocumentApi"/> every method returns an <see cref="ApiResult{T}"/> with a
/// localized message instead of throwing; the status code decides the text, not the server's.
/// </summary>
public sealed class ClassificationApi(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<ClassificationApi> logger)
{
    private const string RequestHeader = "X-NoMorePaper-Request";

    public Task<ApiResult<DocumentFieldsDto>> GetFieldsAsync(Guid id, CancellationToken ct = default)
        => GetAsync<DocumentFieldsDto>($"api/documents/{id}/fields", "fields", ct);

    /// <summary>Saves the fields; 409 (<see cref="ApiResult{T}.IsConflict"/>) when they changed since <see cref="EditFieldsRequestDto.Recognition"/>.</summary>
    public Task<ApiResult<bool>> EditFieldsAsync(Guid id, EditFieldsRequestDto request, CancellationToken ct = default)
        => SendAsync(HttpMethod.Put, $"api/documents/{id}/fields", request, "edit-fields", ChangedMeanwhile, ct);

    /// <summary>Confirms the fields shown with <paramref name="seen"/>; 409 (<see cref="ApiResult{T}.IsConflict"/>) when they changed since.</summary>
    public Task<ApiResult<bool>> ConfirmAsync(Guid id, DocumentRecognitionState seen, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, $"api/documents/{id}/confirm", new ConfirmRequestDto(seen), "confirm", ChangedMeanwhile, ct);

    public Task<ApiResult<bool>> SetEditorsAsync(Guid id, IReadOnlyList<Guid> editorIds, CancellationToken ct = default)
        => SendAsync(HttpMethod.Put, $"api/documents/{id}/editors", new SetEditorsRequestDto(editorIds), "set-editors", null, ct);

    public Task<ApiResult<bool>> KeepDuplicateAsync(Guid id, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, $"api/documents/{id}/duplicate/keep", null, "keep-duplicate", null, ct);

    /// <summary>Deletes a document the server marked as a possible duplicate (owner only).</summary>
    public Task<ApiResult<bool>> DeleteDuplicateAsync(Guid id, CancellationToken ct = default)
        => SendAsync(HttpMethod.Delete, $"api/documents/{id}", null, "delete-duplicate", status =>
            status switch
            {
                403 => localizer["ClsErrorDeleteForbidden"].Value,
                409 => localizer["ClsErrorDeleteConflict"].Value,
                _ => null,
            }, ct);

    public Task<ApiResult<ReviewItemDto[]>> ListReviewAsync(CancellationToken ct = default)
        => GetAsync<ReviewItemDto[]>("api/review", "review", ct);

    /// <summary>Only the number of documents to review, for the navigation.</summary>
    public Task<ApiResult<int>> CountReviewAsync(CancellationToken ct = default)
        => GetAsync<int>("api/review/count", "review-count", ct);

    public Task<ApiResult<EntryDto[]>> ListEntriesAsync(EntryKind kind, CancellationToken ct = default)
        => GetAsync<EntryDto[]>($"api/classification/{EntryKinds.Route(kind)}", "entries", ct);

    /// <summary>Creates an entry, or returns the existing one with the same normalized name.</summary>
    public async Task<ApiResult<EntryDto>> CreateEntryAsync(EntryKind kind, string name, CancellationToken ct = default)
    {
        var result = await PostForAsync<EntryDto>($"api/classification/{EntryKinds.Route(kind)}", new { name }, "create-entry", ct);
        if (result.IsSuccess)
        {
            logger.EntryCreated(kind);
        }

        return result;
    }

    public async Task<ApiResult<bool>> RenameEntryAsync(EntryKind kind, Guid id, string name, CancellationToken ct = default)
    {
        var result = await SendAsync(HttpMethod.Put, $"api/classification/{EntryKinds.Route(kind)}/{id}",
            new RenameEntryDto(name), "rename-entry", ListStatus("ClsErrorRenameForbidden"), ct);
        if (result.IsSuccess)
        {
            logger.EntryRenamed(kind);
        }

        return result;
    }

    public async Task<ApiResult<bool>> MergeEntriesAsync(
        EntryKind kind, Guid targetId, IReadOnlyList<Guid> sourceIds, CancellationToken ct = default)
    {
        var result = await SendAsync(HttpMethod.Post, $"api/classification/{EntryKinds.Route(kind)}/merge",
            new MergeEntriesDto(targetId, sourceIds), "merge-entries", ListStatus("ClsErrorMergeForbidden"), ct);
        if (result.IsSuccess)
        {
            logger.EntriesMerged(kind, sourceIds.Count + 1);
        }

        return result;
    }

    public Task<ApiResult<HouseholdMemberDto[]>> ListHouseholdAsync(CancellationToken ct = default)
        => GetAsync<HouseholdMemberDto[]>("api/household", "household", ct);

    private Func<int, string?> ListStatus(string forbiddenKey) => status => status == 403 ? localizer[forbiddenKey].Value : null;

    private string? ChangedMeanwhile(int status) => status == 409 ? localizer["ClsErrorChangedMeanwhile"].Value : null;

    private async Task<ApiResult<T>> GetAsync<T>(string uri, string operation, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(uri, ct);
            return await ReadAsync<T>(response, operation, null, ct);
        }
        catch (Exception ex) when (IsRecoverable(ex, ct))
        {
            return Unreachable<T>(operation, ex);
        }
    }

    private async Task<ApiResult<T>> PostForAsync<T>(string uri, object body, string operation, CancellationToken ct)
    {
        try
        {
            using var request = Request(HttpMethod.Post, uri, body);
            using var response = await http.SendAsync(request, ct);
            return await ReadAsync<T>(response, operation, null, ct);
        }
        catch (Exception ex) when (IsRecoverable(ex, ct))
        {
            return Unreachable<T>(operation, ex);
        }
    }

    // For calls that answer 204: the result only says whether it worked. A caller-specific text per
    // status (e.g. "not allowed to merge") may replace the general one.
    private async Task<ApiResult<bool>> SendAsync(
        HttpMethod method, string uri, object? body, string operation, Func<int, string?>? special, CancellationToken ct)
    {
        try
        {
            using var request = Request(method, uri, body);
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return new ApiResult<bool>(true, null);
            }

            return Failed<bool>(operation, (int)response.StatusCode, special);
        }
        catch (Exception ex) when (IsRecoverable(ex, ct))
        {
            return Unreachable<bool>(operation, ex);
        }
    }

    private static HttpRequestMessage Request(HttpMethod method, string uri, object? body)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add(RequestHeader, "1");
        return request;
    }

    private async Task<ApiResult<T>> ReadAsync<T>(
        HttpResponseMessage response, string operation, Func<int, string?>? special, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            return Failed<T>(operation, (int)response.StatusCode, special);
        }

        // A non-JSON 200 (e.g. an HTML fallback page) throws JsonException, handled by the caller.
        var value = await response.Content.ReadFromJsonAsync<T>(ct);
        return value is null
            ? new ApiResult<T>(default, localizer["ErrorUnreachable"])
            : new ApiResult<T>(value, null);
    }

    private ApiResult<T> Failed<T>(string operation, int status, Func<int, string?>? special)
    {
        logger.ClassificationCallFailed(operation, status);
        return new ApiResult<T>(default, special?.Invoke(status) ?? MessageFor(status), IsConflict: status == 409);
    }

    private string MessageFor(int status) => status switch
    {
        400 => localizer["ClsErrorInvalid"],
        401 => localizer["ErrorSignInRequired"],
        403 => localizer["ClsErrorForbidden"],
        404 => localizer["ClsErrorNotFound"],
        409 => localizer["ClsErrorConflict"],
        _ => localizer["ErrorServer", status],
    };

    private ApiResult<T> Unreachable<T>(string operation, Exception ex)
    {
        logger.ClassificationUnreachable(operation, ex.GetType().Name);
        return new ApiResult<T>(default, localizer["ErrorUnreachable"]);
    }

    // Caller cancellation (page left) must propagate; everything the network or a bad answer can
    // throw becomes a message.
    private static bool IsRecoverable(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException or JsonException or NotSupportedException or IOException
        || (ex is OperationCanceledException && !ct.IsCancellationRequested);
}
