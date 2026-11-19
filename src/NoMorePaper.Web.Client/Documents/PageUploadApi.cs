using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Documents;
using NoMorePaper.Web.Client.Camera;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Documents;

/// <summary>
/// Client for <c>POST /api/documents/pages</c>: the camera's pages (JPEG, in order) become one document.
/// Like <see cref="DocumentApi"/>, every outcome is an <see cref="ApiResult{T}"/> with a localized message.
/// </summary>
public sealed class PageUploadApi(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<PageUploadApi> logger)
{
    private const string Operation = "pages";

    /// <param name="isPrivate">The person's own choice; <c>null</c> sends none, and the server takes the account's setting.</param>
    /// <param name="takenAt">When the first page was taken, in the phone's local time; names the document.</param>
    public async Task<ApiResult<UploadResultDto>> UploadPagesAsync(
        IReadOnlyList<byte[]> jpegs, bool? isPrivate, DateTimeOffset takenAt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jpegs);
        try
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent(takenAt.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)), "takenAt" },
            };
            if (isPrivate is { } choice)
            {
                form.Add(new StringContent(choice ? "true" : "false"), "private");
            }

            for (var i = 0; i < jpegs.Count; i++)
            {
                var part = new ByteArrayContent(jpegs[i]);
                part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                form.Add(part, "page", string.Create(CultureInfo.InvariantCulture, $"seite-{i + 1:D2}.jpg"));
            }

            form.Headers.Add("X-NoMorePaper-Request", "1");
            using var response = await http.PostAsync("api/documents/pages", form, ct);
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return await DuplicateAsync(response, ct);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.ApiRequestFailed(Operation, (int)response.StatusCode);
                return Fail(MessageFor((int)response.StatusCode));
            }

            var result = await response.Content.ReadFromJsonAsync<UploadResultDto>(ct);
            if (result is null)
            {
                return Fail(localizer["ErrorUnreachable"]);
            }

            logger.PagesUploaded(jpegs.Count);
            return new ApiResult<UploadResultDto>(result, null);
        }
        catch (Exception ex) when (IsRecoverable(ex, ct))
        {
            logger.ApiUnreachable(Operation, ex.GetType().Name);
            return Fail(localizer["ErrorUnreachable"]);
        }
    }

    // 409: the very same PDF exists already; the ProblemDetails names it in "existingId".
    private async Task<ApiResult<UploadResultDto>> DuplicateAsync(HttpResponseMessage response, CancellationToken ct)
    {
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
            // No readable extension: the message still says the document exists.
        }

        return new ApiResult<UploadResultDto>(null, localizer["UploadDuplicate"], ExistingId: existing, IsDuplicate: true);
    }

    private string MessageFor(int status) => status switch
    {
        400 => localizer["CamErrorRejected"],
        401 => localizer["ErrorSignInRequired"],
        413 => localizer["CamErrorTooLarge"],
        _ => localizer["ErrorServer", status],
    };

    private static ApiResult<UploadResultDto> Fail(string error) => new(null, error);

    // Caller cancellation (page left) must propagate; everything the network or a bad answer can
    // throw becomes a message.
    private static bool IsRecoverable(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException or JsonException or NotSupportedException or IOException
        || (ex is OperationCanceledException && !ct.IsCancellationRequested);
}
