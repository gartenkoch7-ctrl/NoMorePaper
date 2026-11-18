namespace ICloudSpike.Http;

/// <summary>A failed protocol step. The message never contains credentials, tokens or user content.</summary>
internal sealed class ICloudException : Exception
{
    public ICloudException(
        ICloudFailure failure,
        string message,
        int? statusCode = null,
        string? appleCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Failure = failure;
        StatusCode = statusCode;
        AppleCode = appleCode;
    }

    public ICloudFailure Failure { get; }

    public int? StatusCode { get; }

    /// <summary>Apple's own error code from the response body, e.g. "-21669" for a wrong code.</summary>
    public string? AppleCode { get; }
}
