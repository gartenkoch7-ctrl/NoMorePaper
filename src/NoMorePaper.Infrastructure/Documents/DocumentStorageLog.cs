using Microsoft.Extensions.Logging;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Infrastructure.Documents;

/// <summary>
/// Log events of the document storage (Infrastructure 3100–3199, docs/kb/telemetrie.md). Only ids,
/// kinds, sizes and exception types: no file names, paths or exception messages, which contain paths.
/// </summary>
internal static partial class DocumentStorageLog
{
    [LoggerMessage(EventId = 3100, Level = LogLevel.Information, Message = "Stored the {Kind} of document {DocumentId} in the {Area} ({SizeBytes} bytes)")]
    public static partial void Stored(this ILogger logger, DocumentFileKind kind, Guid documentId, string area, long sizeBytes);

    [LoggerMessage(EventId = 3101, Level = LogLevel.Warning, Message = "Storing the {Kind} of document {DocumentId} failed ({ErrorType}); the partial copy was discarded")]
    public static partial void StoreFailed(this ILogger logger, DocumentFileKind kind, Guid documentId, string errorType);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Information, Message = "Moved the original of document {DocumentId} from the inbox to the archive")]
    public static partial void OriginalArchived(this ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 3103, Level = LogLevel.Warning, Message = "Could not clean up leftovers of document {DocumentId} in the {Area} ({ErrorType})")]
    public static partial void CleanupFailed(this ILogger logger, Guid documentId, string area, string errorType);
}
