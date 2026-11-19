using Microsoft.Extensions.Logging;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Documents;

/// <summary>
/// Log events of document intake (docs/kb/telemetrie.md). The ids 3200–3204 come from the
/// Infrastructure range, where intake lived in M1, and stay stable; later ones are in the Application
/// range (5280, inputs 5300–5399). Only ids, sizes, sources and exception types: no file names,
/// titles, senders or subjects.
/// </summary>
internal static partial class DocumentIntakeLog
{
    [LoggerMessage(EventId = 3200, Level = LogLevel.Information, Message = "Received document {DocumentId} from {Source} ({SizeBytes} bytes, {MediaType}) and queued it for processing")]
    public static partial void Received(this ILogger logger, Guid documentId, DocumentSource source, long sizeBytes, string mediaType);

    [LoggerMessage(EventId = 3201, Level = LogLevel.Error, Message = "Receiving document {DocumentId} from {Source} failed ({ErrorType})")]
    public static partial void ReceiveFailed(this ILogger logger, Guid documentId, DocumentSource source, string errorType);

    [LoggerMessage(EventId = 3202, Level = LogLevel.Warning, Message = "Could not remove the received file of document {DocumentId} after a failure ({ErrorType})")]
    public static partial void DiscardFailed(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 3203, Level = LogLevel.Warning, Message = "Document {DocumentId} was recorded although saving reported a failure; its received file is kept")]
    public static partial void KeptRecorded(this ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 3204, Level = LogLevel.Warning, Message = "Could not check whether document {DocumentId} was recorded ({ErrorType}); its received file is kept")]
    public static partial void KeptUnverified(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 5280, Level = LogLevel.Information, Message = "Upload of account {AccountId} refused: the same file is document {ExistingDocumentId}")]
    public static partial void DuplicateRefused(this ILogger logger, Guid existingDocumentId, Guid accountId);

    [LoggerMessage(EventId = 5300, Level = LogLevel.Information, Message = "File from {Source} dropped: its owner {AccountId} has it already as document {ExistingDocumentId}")]
    public static partial void DuplicateDropped(this ILogger logger, DocumentSource source, Guid accountId, Guid existingDocumentId);

    [LoggerMessage(EventId = 5301, Level = LogLevel.Warning, Message = "File from {Source} not taken: its owner {AccountId} was removed")]
    public static partial void OwnerGone(this ILogger logger, DocumentSource source, Guid accountId);

    [LoggerMessage(EventId = 5302, Level = LogLevel.Warning, Message = "File from {Source} not taken: its owner is unknown and there is no administrator")]
    public static partial void NoAdministrator(this ILogger logger, DocumentSource source);

    [LoggerMessage(EventId = 5303, Level = LogLevel.Information, Message = "Account {AccountId} handed document {DocumentId} over to account {NewOwnerId} (recognition again: {Requeued})")]
    public static partial void HandedOver(this ILogger logger, Guid accountId, Guid documentId, Guid newOwnerId, bool requeued);
}
