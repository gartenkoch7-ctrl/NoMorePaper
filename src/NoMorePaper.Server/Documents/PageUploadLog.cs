namespace NoMorePaper.Server.Documents;

/// <summary>
/// Log events of the camera page upload (Server 1500–1599, docs/kb/telemetrie.md). Only counts, sizes,
/// fixed reason words and exception types; never image data or file names.
/// </summary>
internal static partial class PageUploadLog
{
    [LoggerMessage(EventId = 1500, Level = LogLevel.Information,
        Message = "Camera pages accepted as document {DocumentId} ({Pages} pages, {SizeBytes} bytes)")]
    public static partial void PagesAccepted(this ILogger logger, Guid documentId, int pages, long sizeBytes);

    [LoggerMessage(EventId = 1501, Level = LogLevel.Warning, Message = "Camera pages rejected: {Reason}")]
    public static partial void PagesRejected(this ILogger logger, string reason);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Warning,
        Message = "Camera pages could not be made into a PDF ({Pages} pages): {ExceptionType}")]
    public static partial void PagesPdfFailed(this ILogger logger, int pages, string exceptionType);
}
