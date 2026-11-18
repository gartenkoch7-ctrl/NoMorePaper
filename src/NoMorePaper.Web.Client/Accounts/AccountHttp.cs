using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NoMorePaper.Web.Client.Documents;
using NoMorePaper.Web.Client.Localization;

namespace NoMorePaper.Web.Client.Accounts;

/// <summary>
/// Sends the account calls and turns every outcome into an <see cref="ApiResult{T}"/> with a
/// localized message: the status code decides, the server's ProblemDetails text is never shown
/// (it is not localized). Non-GET calls carry the header the server's cross-site guard requires.
/// </summary>
internal sealed class AccountHttp(HttpClient http, IStringLocalizer<Strings> localizer, ILogger<AccountApi> logger)
{
    public const string RequestHeader = "X-NoMorePaper-Request";

    public async Task<ApiResult<T>> SendAsync<T>(
        HttpMethod method, string uri, object? body, string operation, CancellationToken ct)
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
                logger.AccountApiFailed(operation, status);
                if (status == 400 && await RefusedInput.ReadAsync(response, ct) is { } refused)
                {
                    return new ApiResult<T>(default, localizer[refused.MessageKey, FormValidation.MinPasswordLength], refused.Field);
                }

                return new ApiResult<T>(default, MessageFor(operation, status, response));
            }

            if (typeof(T) == typeof(bool))
            {
                return new ApiResult<T>((T)(object)true, null);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(ct);
            return value is null
                ? new ApiResult<T>(default, localizer["ErrorUnreachable"])
                : new ApiResult<T>(value, null);
        }
        catch (Exception ex) when (IsRecoverable(ex, ct))
        {
            logger.AccountApiUnreachable(operation, ex.GetType().Name);
            return new ApiResult<T>(default, localizer["ErrorUnreachable"]);
        }
    }

    private string MessageFor(string operation, int status, HttpResponseMessage response) => status switch
    {
        423 => LockedMessage(response),
        401 when operation == "login" => localizer["ErrorLoginFailed"],
        401 when operation == "emergency-reset" => localizer["ErrorEmergencyFailed"],
        401 => localizer["ErrorSignInRequired"],
        400 => localizer["ErrorRejected"],
        403 => localizer["ErrorForbidden"],
        404 when operation is "invitation" or "accept" or "password-reset" => localizer["ErrorLinkInvalid"],
        404 => localizer["ErrorNotFound"],
        409 => localizer["ErrorAlreadySetUp"],
        _ => localizer["ErrorServer", status],
    };

    // Retry-After in seconds; rounded up so "0 minutes" is never said while still locked.
    private string LockedMessage(HttpResponseMessage response)
    {
        var seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds;
        if (seconds is null or <= 0)
        {
            return localizer["ErrorLockedUnknown"];
        }

        var minutes = (int)Math.Ceiling(seconds.Value / 60);
        return minutes == 1
            ? localizer["ErrorLockedOne"]
            : localizer["ErrorLocked", minutes.ToString(CultureInfo.CurrentCulture)];
    }

    private static bool IsRecoverable(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException or JsonException or NotSupportedException or IOException
        || (ex is OperationCanceledException && !ct.IsCancellationRequested);
}
