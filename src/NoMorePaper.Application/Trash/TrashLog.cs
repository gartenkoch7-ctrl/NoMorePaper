using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Trash;

/// <summary>
/// Log events of the trash (Application 5600–5699, docs/kb/telemetrie.md). Ids, kinds and counts only:
/// never titles or file names.
/// </summary>
internal static partial class TrashLog
{
    [LoggerMessage(EventId = 5600, Level = LogLevel.Information, Message = "Owner {AccountId} moved document {DocumentId} to the trash as entry {TrashItemId}")]
    public static partial void DocumentTrashed(this ILogger logger, Guid documentId, Guid accountId, Guid trashItemId);

    [LoggerMessage(EventId = 5601, Level = LogLevel.Information, Message = "The job and planned edit of document {DocumentId} were dropped, because it went to the trash")]
    public static partial void PendingWorkDropped(this ILogger logger, Guid documentId);
}
