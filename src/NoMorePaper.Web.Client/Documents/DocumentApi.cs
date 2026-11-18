using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Documents;
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

    public async Task<ApiResult<UploadResultDto>> UploadAsync(IBrowserFile file, CancellationToken ct = default)
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
            using var form = new MultipartFormDataContent { { part, "file", file.Name } };
            using var response = await http.PostAsync("api/documents", form, ct);
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

    public Task<ApiResult<DocumentSummaryDto[]>> ListAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
        => GetAsync<DocumentSummaryDto[]>($"api/documents?page={page}&pageSize={pageSize}", "list", ct);

    public Task<ApiResult<DocumentDetailDto>> GetAsync(Guid id, CancellationToken ct = default)
        => GetAsync<DocumentDetailDto>($"api/documents/{id}", "detail", ct);

    public Task<ApiResult<SearchHitDto[]>> SearchAsync(string query, int limit = 20, CancellationToken ct = default)
        => GetAsync<SearchHitDto[]>($"api/search?q={Uri.EscapeDataString(query)}&limit={limit}", "search", ct);

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
