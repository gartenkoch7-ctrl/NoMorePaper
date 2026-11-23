using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Classification;
using NoMorePaper.Application.Processing;

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

    [LoggerMessage(EventId = 5610, Level = LogLevel.Information, Message = "Owner {AccountId} restored trash entry {TrashItemId} ({Kind}) of document {DocumentId}")]
    public static partial void EntryRestored(this ILogger logger, Guid trashItemId, TrashItemKind kind, Guid documentId, Guid accountId);

    [LoggerMessage(EventId = 5611, Level = LogLevel.Information, Message = "The edited version document {DocumentId} kept went to the trash as entry {TrashItemId}, swapped for the restored one")]
    public static partial void VersionSwapped(this ILogger logger, Guid documentId, Guid trashItemId);

    [LoggerMessage(EventId = 5612, Level = LogLevel.Information, Message = "Document {DocumentId} came back from the trash; its {Stage} job, dropped with the move, was queued again")]
    public static partial void WorkRequeued(this ILogger logger, Guid documentId, JobStage stage);

    [LoggerMessage(EventId = 5613, Level = LogLevel.Information, Message = "The archive of document {DocumentId} is made anew from its restored edited version")]
    public static partial void RebuildQueued(this ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 5614, Level = LogLevel.Information, Message = "Document {DocumentId} keeps both versions again, because its {Kind} came back from the trash")]
    public static partial void BothVersionsKept(this ILogger logger, Guid documentId, TrashItemKind kind);

    [LoggerMessage(EventId = 5615, Level = LogLevel.Warning, Message = "Account {AccountId} was refused ({Outcome}): {Action} of trash entry {TrashItemId} ({Reason})")]
    public static partial void TrashChangeRefused(this ILogger logger, Guid accountId, ChangeOutcome outcome, string action, Guid trashItemId, string reason);

    [LoggerMessage(EventId = 5616, Level = LogLevel.Error, Message = "Restoring trash entry {TrashItemId} of document {DocumentId} failed ({ExceptionType}); its files were moved back")]
    public static partial void RestoreUndone(this ILogger logger, Guid trashItemId, Guid documentId, string exceptionType);

    [LoggerMessage(EventId = 5617, Level = LogLevel.Error, Message = "Restoring trash entry {TrashItemId} of document {DocumentId} failed and its files could not be moved back ({ExceptionType}); check the archive folder")]
    public static partial void RestoreUndoFailed(this ILogger logger, Guid trashItemId, Guid documentId, string exceptionType);

    [LoggerMessage(EventId = 5618, Level = LogLevel.Information, Message = "Trash entry {TrashItemId} ({Kind}) of document {DocumentId} was removed for good")]
    public static partial void EntryErased(this ILogger logger, Guid trashItemId, TrashItemKind kind, Guid documentId);

    [LoggerMessage(EventId = 5619, Level = LogLevel.Information, Message = "Document {DocumentId} was deleted for good with {EntryCount} trash entries")]
    public static partial void DocumentErased(this ILogger logger, Guid documentId, int entryCount);

    [LoggerMessage(EventId = 5620, Level = LogLevel.Warning, Message = "Files of {Id} were left after removing it for good ({ExceptionType}); they only cost space")]
    public static partial void ErasedFilesLeft(this ILogger logger, Guid id, string exceptionType);

    [LoggerMessage(EventId = 5621, Level = LogLevel.Information, Message = "Owner {AccountId} removed trash entry {TrashItemId} for good")]
    public static partial void EntryPurged(this ILogger logger, Guid trashItemId, Guid accountId);

    [LoggerMessage(EventId = 5622, Level = LogLevel.Information, Message = "Owner {AccountId} emptied the trash: {Count} entries removed for good")]
    public static partial void TrashEmptied(this ILogger logger, Guid accountId, int count);

    [LoggerMessage(EventId = 5623, Level = LogLevel.Information, Message = "{Count} trash entries were due and removed for good")]
    public static partial void DueTrashPurged(this ILogger logger, int count);

    [LoggerMessage(EventId = 5624, Level = LogLevel.Warning, Message = "Trash entry {TrashItemId} of document {DocumentId} named a document that is not in the trash; only the entry was removed")]
    public static partial void StaleDocumentEntry(this ILogger logger, Guid trashItemId, Guid documentId);
}
