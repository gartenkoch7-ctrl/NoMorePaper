using System.Text.Json;

namespace ICloudSpike.Http;

/// <summary>Error code and text from an Apple response body, wherever Apple happened to put them.</summary>
internal sealed record AppleError(string? Code, string? Message)
{
    /// <summary>Service error for a wrong verification code.</summary>
    public const string WrongSecurityCode = "-21669";

    /// <summary>Service error for a rejected Apple-ID or password.</summary>
    public const string WrongCredentials = "-20101";

    /// <summary>Service error for a locked account.</summary>
    public const string AccountLocked = "-20209";

    private const int MaxMessageLength = 160;

    public static AppleError From(ApiResponse response)
    {
        var root = response.Json;
        if (root is null)
        {
            return new AppleError(null, null);
        }

        // The sign-in endpoints use "serviceErrors", the verification endpoints "service_errors".
        foreach (var listName in (string[])["serviceErrors", "service_errors"])
        {
            foreach (var entry in root.Child(listName).Items())
            {
                JsonElement? first = entry;
                return new AppleError(first.Child("code").Text(), Shorten(first.Child("message").Text()));
            }
        }

        var code = root.Child("errorCode").Text() ?? root.Child("serverErrorCode").Text();
        var message = root.Child("errorMessage").Text()
            ?? root.Child("reason").Text()
            ?? root.Child("errorReason").Text()
            ?? root.Child("error").Text();
        return new AppleError(code, Shorten(message));
    }

    private static string? Shorten(string? message) =>
        message is { Length: > MaxMessageLength } ? message[..MaxMessageLength] + "…" : message;
}
