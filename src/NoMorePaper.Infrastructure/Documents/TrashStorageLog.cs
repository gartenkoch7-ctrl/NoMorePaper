using Microsoft.Extensions.Logging;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Log events of the trash folders and the page images of edits (Infrastructure 3970–3975,
/// docs/kb/telemetrie.md). Ids, kinds and page numbers only: never file names or paths.
/// </summary>
internal static partial class TrashStorageLog
{
    [LoggerMessage(EventId = 3970, Level = LogLevel.Information, Message = "Moved the {Kind} of document {DocumentId} to trash entry {TrashItemId}")]
    public static partial void MovedToTrash(this ILogger logger, DocumentFileKind kind, Guid documentId, Guid trashItemId);

    [LoggerMessage(EventId = 3971, Level = LogLevel.Information, Message = "Moved the {Kind} of document {DocumentId} back from trash entry {TrashItemId}")]
    public static partial void RestoredFromTrash(this ILogger logger, DocumentFileKind kind, Guid documentId, Guid trashItemId);

    [LoggerMessage(EventId = 3972, Level = LogLevel.Information, Message = "Removed the files of trash entry {TrashItemId} for good")]
    public static partial void TrashPurged(this ILogger logger, Guid trashItemId);

    [LoggerMessage(EventId = 3973, Level = LogLevel.Information, Message = "Stored the image replacing page {Page} of document {DocumentId} ({SizeBytes} bytes)")]
    public static partial void EditPageStored(this ILogger logger, int page, Guid documentId, long sizeBytes);

    [LoggerMessage(EventId = 3974, Level = LogLevel.Information, Message = "Discarded the page images of the edit of document {DocumentId}")]
    public static partial void EditPagesDiscarded(this ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 3975, Level = LogLevel.Warning, Message = "Storing an image for page {Page} of document {DocumentId} failed ({ErrorType}); the partial copy was discarded")]
    public static partial void EditPageStoreFailed(this ILogger logger, int page, Guid documentId, string errorType);
}
