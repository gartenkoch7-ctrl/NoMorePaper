using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Log events of document intake (Infrastructure 3200–3299, docs/kb/telemetrie.md).
/// Only ids, sizes and exception types: no file names or titles.
/// </summary>
internal static partial class DocumentIntakeLog
{
    [LoggerMessage(EventId = 3200, Level = LogLevel.Information, Message = "Received document {DocumentId} ({SizeBytes} bytes, {MediaType}) and queued it for processing")]
    public static partial void Received(this ILogger logger, Guid documentId, long sizeBytes, string mediaType);

    [LoggerMessage(EventId = 3201, Level = LogLevel.Error, Message = "Receiving document {DocumentId} failed ({ErrorType}); the received file was discarded")]
    public static partial void ReceiveFailed(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 3202, Level = LogLevel.Warning, Message = "Could not remove the received file of document {DocumentId} after a failure ({ErrorType})")]
    public static partial void DiscardFailed(this ILogger logger, Guid documentId, string errorType);
}
