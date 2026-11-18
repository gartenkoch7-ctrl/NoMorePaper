using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Documents;

/// <summary>
/// Log events of the document pages (EventId range 4100–4199, see docs/kb/telemetrie.md).
/// Only sizes, counts and HTTP status codes are logged, never file names, titles or search terms.
/// </summary>
internal static partial class DocumentsLog
{
    [LoggerMessage(EventId = 4100, EventName = "UploadStarted", Level = LogLevel.Information,
        Message = "Upload of {SizeBytes} bytes started")]
    public static partial void UploadStarted(this ILogger logger, long sizeBytes);

    [LoggerMessage(EventId = 4101, EventName = "UploadAccepted", Level = LogLevel.Information,
        Message = "Upload accepted by the server")]
    public static partial void UploadAccepted(this ILogger logger);

    [LoggerMessage(EventId = 4102, EventName = "UploadRejected", Level = LogLevel.Warning,
        Message = "Upload rejected: {Reason}")]
    public static partial void UploadRejected(this ILogger logger, string reason);

    [LoggerMessage(EventId = 4103, EventName = "ApiRequestFailed", Level = LogLevel.Warning,
        Message = "API call {Operation} failed with HTTP status {StatusCode}")]
    public static partial void ApiRequestFailed(this ILogger logger, string operation, int statusCode);

    [LoggerMessage(EventId = 4104, EventName = "ApiUnreachable", Level = LogLevel.Warning,
        Message = "API call {Operation} failed: {ExceptionType}")]
    public static partial void ApiUnreachable(this ILogger logger, string operation, string exceptionType);

    [LoggerMessage(EventId = 4105, EventName = "DocumentListLoaded", Level = LogLevel.Debug,
        Message = "Document list loaded: {Count} entries")]
    public static partial void DocumentListLoaded(this ILogger logger, int count);

    [LoggerMessage(EventId = 4106, EventName = "DocumentStateChanged", Level = LogLevel.Information,
        Message = "A document changed its processing state to {State}")]
    public static partial void DocumentStateChanged(this ILogger logger, string state);

    [LoggerMessage(EventId = 4107, EventName = "SearchCompleted", Level = LogLevel.Information,
        Message = "Search completed with {HitCount} hits")]
    public static partial void SearchCompleted(this ILogger logger, int hitCount);

    [LoggerMessage(EventId = 4108, EventName = "ListPollingStarted", Level = LogLevel.Debug,
        Message = "Polling the document list while documents are in progress")]
    public static partial void ListPollingStarted(this ILogger logger);
}
