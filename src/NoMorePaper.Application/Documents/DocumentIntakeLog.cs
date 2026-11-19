using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Documents;

/// <summary>
/// Log events of document intake (docs/kb/telemetrie.md). The ids 3200–3204 come from the
/// Infrastructure range, where intake lived in M1, and stay stable; later ones are in the Application
/// range. Only ids, sizes and exception types: no file names or titles.
/// </summary>
internal static partial class DocumentIntakeLog
{
    [LoggerMessage(EventId = 3200, Level = LogLevel.Information, Message = "Received document {DocumentId} ({SizeBytes} bytes, {MediaType}) and queued it for processing")]
    public static partial void Received(this ILogger logger, Guid documentId, long sizeBytes, string mediaType);

    [LoggerMessage(EventId = 3201, Level = LogLevel.Error, Message = "Receiving document {DocumentId} failed ({ErrorType})")]
    public static partial void ReceiveFailed(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 3202, Level = LogLevel.Warning, Message = "Could not remove the received file of document {DocumentId} after a failure ({ErrorType})")]
    public static partial void DiscardFailed(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 3203, Level = LogLevel.Warning, Message = "Document {DocumentId} was recorded although saving reported a failure; its received file is kept")]
    public static partial void KeptRecorded(this ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 5280, Level = LogLevel.Information, Message = "Upload of account {AccountId} refused: the same file is document {ExistingDocumentId}")]
    public static partial void DuplicateRefused(this ILogger logger, Guid existingDocumentId, Guid accountId);

    [LoggerMessage(EventId = 3204,Level = LogLevel.Warning, Message = "Could not check whether document {DocumentId} was recorded ({ErrorType}); its received file is kept")]
    public static partial void KeptUnverified(this ILogger logger, Guid documentId, string errorType);
}
