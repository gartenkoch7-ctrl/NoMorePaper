using System.Globalization;

namespace ICloudSpike.Http;

/// <summary>Maps an unexpected HTTP answer to an <see cref="ICloudException"/>.</summary>
internal static class ResponseErrors
{
    /// <param name="step">Short English name of the protocol step, e.g. "signin/complete".</param>
    /// <param name="response">The answer that was not accepted.</param>
    /// <param name="failure">Set when the caller knows better than the generic status mapping.</param>
    public static ICloudException Create(string step, ApiResponse response, ICloudFailure? failure = null)
    {
        var error = AppleError.From(response);
        var kind = failure ?? Classify(response.Status, error);
        var message = string.Create(CultureInfo.InvariantCulture, $"{step}: HTTP {response.Status}");
        if (error.Code is not null)
        {
            message += $", Apple code {error.Code}";
        }

        if (error.Message is not null)
        {
            message += $" ({error.Message})";
        }

        return new ICloudException(kind, message, response.Status, error.Code);
    }

    /// <summary>Status codes that mean the same on every endpoint.</summary>
    public static ICloudFailure Classify(int status, AppleError error) => status switch
    {
        _ when error.Code == AppleError.AccountLocked => ICloudFailure.AccountLocked,
        421 or 450 => ICloudFailure.SessionExpired,
        429 or 503 => ICloudFailure.RateLimited,
        _ => ICloudFailure.Unexpected,
    };
}
