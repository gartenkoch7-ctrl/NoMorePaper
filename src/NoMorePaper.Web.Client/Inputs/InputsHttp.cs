using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Web.Client.Accounts;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Inputs;

/// <summary>
/// Sends the calls of the input clients and turns every outcome into an <see cref="ApiResult{T}"/> with a
/// localized message; the status code decides, the server's text is never shown. Non-GET calls carry the
/// header the server's cross-site guard requires.
/// </summary>
internal sealed class InputsHttp(HttpClient http, IStringLocalizer<Strings> localizer, ILogger logger)
{
    private const string RequestHeader = "X-NoMorePaper-Request";

    /// <param name="special">A caller's own text for a status (e.g. 409 "taken"), or <c>null</c> for the general one.</param>
    /// <param name="field">The request field a caller's own text is about, reported in <see cref="ApiResult{T}.Field"/>.</param>
    public Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string uri, object? body, string operation, Func<int, string?>? special, string? field, CancellationToken ct)
        => SendAsync<T>(method, uri, body, operation, special, field, null, ct);

    /// <param name="refused">
    /// For a 400 with <c>errors: { field: [word] }</c>: the caller's text and the request field it is about, or
    /// <c>null</c> to fall back to <paramref name="special"/>.
    /// </param>
    public async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string uri, object? body, string operation, Func<int, string?>? special, string? field,
        Func<RefusedInput, (string Text, string Field)?>? refused, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (method != HttpMethod.Get)
            {
                request.Headers.Add(RequestHeader, "1");
            }

            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType());
            }

            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                logger.InputsCallFailed(operation, status);
                if (status == 400 && refused is not null && await RefusedInput.ReadAsync(response, ct) is { } input && refused(input) is { } named)
                {
                    return new ApiResult<T>(default, named.Text, named.Field);
                }

                var own = special?.Invoke(status);
                return new ApiResult<T>(default, own ?? MessageFor(status), own is null ? null : field, IsConflict: status == 409);
            }

            if (typeof(T) == typeof(bool))
            {
                return new ApiResult<T>((T)(object)true, null);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(ct);
            return value is null ? new ApiResult<T>(default, localizer["ErrorUnreachable"]) : new ApiResult<T>(value, null);
        }
        catch (Exception ex) when (IsRecoverable(ex, ct))
        {
            logger.InputsUnreachable(operation, ex.GetType().Name);
            return new ApiResult<T>(default, localizer["ErrorUnreachable"]);
        }
    }

    private string MessageFor(int status) => status switch
    {
        400 => localizer["ErrorRejected"],
        401 => localizer["ErrorSignInRequired"],
        403 => localizer["ErrorForbidden"],
        404 => localizer["ErrorNotFound"],
        _ => localizer["ErrorServer", status],
    };

    // Caller cancellation (page left) must propagate; everything the network or a bad answer can throw
    // becomes a message.
    private static bool IsRecoverable(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException or JsonException or NotSupportedException or IOException
        || (ex is OperationCanceledException && !ct.IsCancellationRequested);
}
