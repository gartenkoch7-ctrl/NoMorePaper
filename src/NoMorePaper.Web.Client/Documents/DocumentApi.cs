using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Accounts;
using NoMorePaper.Contracts.Classification;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Web.Client.Classification;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Documents;

/// <summary>
/// Client for the document API. Every method returns an <see cref="ApiResult{T}"/>: server
/// problems become a localized message instead of an exception, so pages never crash.
/// The server's ProblemDetails text is not shown (it is not localized); the status code decides.
/// </summary>
public sealed class DocumentApi(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<DocumentApi> logger)
{
    public const long MaxUploadBytes = 100L * 1024 * 1024;

    /// <param name="isPrivate">The person's own choice; <c>null</c> sends none, and the server takes the account's setting.</param>
    public async Task<ApiResult<UploadResultDto>> UploadAsync(IBrowserFile file, bool? isPrivate = null, CancellationToken ct = default)
    {
        if (file.Size == 0)
        {
            logger.UploadRejected("empty");
            return Fail<UploadResultDto>(localizer["ErrorEmpty"]);
        }

        if (file.Size > MaxUploadBytes)
        {
            logger.UploadRejected("too-large");
            return Fail<UploadResultDto>(localizer["ErrorTooLarge"]);
        }

        logger.UploadStarted(file.Size);
        try
        {
            await using var stream = file.OpenReadStream(MaxUploadBytes, ct);
            using var part = new StreamContent(stream);
            part.Headers.ContentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var type)
                ? type
                : new MediaTypeHeaderValue("application/octet-stream");
            // The privacy field goes first so a server reading the form as a stream knows it before the file.
            using var form = new MultipartFormDataContent();
            if (isPrivate is { } choice)
            {
                form.Add(new StringContent(choice ? "true" : "false"), "private");
            }

            form.Add(part, "file", file.Name);
            form.Headers.Add("X-NoMorePaper-Request", "1");
            using var response = await http.PostAsync("api/documents", form, ct);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return await DuplicateAsync(response, ct);
            }

            var result = await ReadAsync<UploadResultDto>(response, "upload", ct);
            if (result.IsSuccess)
            {
                logger.UploadAccepted();
            }

            return result;
        }
        catch (Exception ex) when (IsRecoverable(ex, ct))
        {
            return Unreachable<UploadResultDto>("upload", ex);
        }
    }

    public Task<ApiResult<DocumentSummaryDto[]>> ListAsync(
        int page = 1, int pageSize = 50, DocumentFilterDto? filter = null, CancellationToken ct = default)
        => GetAsync<DocumentSummaryDto[]>(
            $"api/documents?page={page}&pageSize={pageSize}{DocumentFilterQuery.Build(filter)}", "list", ct);

    /// <summary>Counts for the list header and the type chips, with the same filters as <see cref="ListAsync"/>.</summary>
    public Task<ApiResult<DocumentCountsDto>> CountAsync(DocumentFilterDto? filter, CancellationToken ct = default)
    {
        var query = DocumentFilterQuery.Build(filter);
        return GetAsync<DocumentCountsDto>(query.Length == 0 ? "api/documents/counts" : "api/documents/counts?" + query[1..], "counts", ct);
    }

    public Task<ApiResult<DocumentDetailDto>> GetAsync(Guid id, CancellationToken ct = default)
        => GetAsync<DocumentDetailDto>($"api/documents/{id}", "detail", ct);

    public Task<ApiResult<SearchHitDto[]>> SearchAsync(string query, int limit = 20, CancellationToken ct = default)
        => GetAsync<SearchHitDto[]>($"api/search?q={Uri.EscapeDataString(query)}&limit={limit}", "search", ct);

    /// <summary>Makes a document private or shared again; only its owner may (anyone else gets "not found").</summary>
    public async Task<ApiResult<bool>> SetPrivacyAsync(Guid id, bool isPrivate, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/documents/{id}")
            {
                Content = JsonContent.Create(new DocumentPrivacyDto(isPrivate)),
            };
            request.Headers.Add("X-NoMorePaper-Request", "1");
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.ApiRequestFailed("privacy", (int)response.StatusCode);
                return Fail<bool>(MessageFor((int)response.StatusCode));
            }

            logger.PrivacyChanged(isPrivate);
            return Ok(true);
        }
        catch (Exception ex) when (IsRecoverable(ex, ct))
        {
            return Unreachable<bool>("privacy", ex);
        }
    }

    // 409 on upload: the same file is already there. The ProblemDetails names it in "existingId".
    private async Task<ApiResult<UploadResultDto>> DuplicateAsync(HttpResponseMessage response, CancellationToken ct)
    {
        logger.UploadRejected("duplicate");
        Guid? existing = null;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (problem.ValueKind == JsonValueKind.Object
                && problem.TryGetProperty("existingId", out var id)
                && id.ValueKind == JsonValueKind.String
                && id.TryGetGuid(out var parsed))
            {
                existing = parsed;
            }
        }
        catch (JsonException)
        {
            // No readable extension: the message still says the file exists.
        }

        return new ApiResult<UploadResultDto>(null, localizer["UploadDuplicate"], ExistingId: existing, IsDuplicate: true);
    }

    private async Task<ApiResult<T>> GetAsync<T>(string uri, string operation, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(uri, ct);
            return await ReadAsync<T>(response, operation, ct);
        }
        catch (Exception ex) when (IsRecoverable(ex, ct))
        {
            return Unreachable<T>(operation, ex);
        }
    }

    private async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage response, string operation, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode)
        {
            logger.ApiRequestFailed(operation, status);
            return Fail<T>(MessageFor(status));
        }

        // A non-JSON 200 (e.g. an HTML fallback page) throws JsonException, handled by the caller.
        var value = await response.Content.ReadFromJsonAsync<T>(ct);
        return value is null
            ? Fail<T>(localizer["ErrorUnreachable"])
            : Ok(value);
    }

    private static ApiResult<T> Ok<T>(T value) => new(value, null);

    private static ApiResult<T> Fail<T>(string error) => new(default, error);

    private string MessageFor(int status) => status switch
    {
        400 => localizer["ErrorEmpty"],
        401 => localizer["ErrorSignInRequired"],
        404 => localizer["ErrorNotFound"],
        413 => localizer["ErrorTooLarge"],
        415 => localizer["ErrorType"],
        _ => localizer["ErrorServer", status],
    };

    private ApiResult<T> Unreachable<T>(string operation, Exception ex)
    {
        logger.ApiUnreachable(operation, ex.GetType().Name);
        return Fail<T>(localizer["ErrorUnreachable"]);
    }

    // Caller cancellation (page left) must propagate; everything the network or a bad answer can
    // throw becomes a message.
    private static bool IsRecoverable(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException or JsonException or NotSupportedException or IOException
        || (ex is OperationCanceledException && !ct.IsCancellationRequested);
}
