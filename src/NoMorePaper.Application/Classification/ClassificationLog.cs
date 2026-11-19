using Microsoft.Extensions.Logging;
using NoMorePaper.Domain.Classification;

namespace NoMorePaper.Application.Classification;

/// <summary>
/// Log events of fields, editors, duplicates and lists (Application 5250–5279, docs/kb/telemetrie.md).
/// Ids, kinds and counts only: never entry names, titles, summaries or other fields.
/// </summary>
internal static partial class ClassificationLog
{
    [LoggerMessage(EventId = 5250, Level = LogLevel.Information, Message = "Account {AccountId} edited the fields of document {DocumentId}")]
    public static partial void FieldsEdited(this ILogger logger, Guid documentId, Guid accountId);

    [LoggerMessage(EventId = 5251, Level = LogLevel.Information, Message = "Account {AccountId} confirmed the fields of document {DocumentId}")]
    public static partial void FieldsConfirmed(this ILogger logger, Guid documentId, Guid accountId);

    [LoggerMessage(EventId = 5252, Level = LogLevel.Information, Message = "Owner {AccountId} set {EditorCount} editors of document {DocumentId}")]
    public static partial void EditorsSet(this ILogger logger, Guid documentId, Guid accountId, int editorCount);

    [LoggerMessage(EventId = 5253, Level = LogLevel.Information, Message = "Account {AccountId} kept document {DocumentId} despite the duplicate hint")]
    public static partial void DuplicateKept(this ILogger logger, Guid documentId, Guid accountId);

    [LoggerMessage(EventId = 5254, Level = LogLevel.Information, Message = "Owner {AccountId} deleted document {DocumentId}, a possible duplicate")]
    public static partial void DuplicateDeleted(this ILogger logger, Guid documentId, Guid accountId);

    [LoggerMessage(EventId = 5255, Level = LogLevel.Warning, Message = "Could not remove the files of deleted document {DocumentId} ({ErrorType})")]
    public static partial void DeletedFilesLeft(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 5256, Level = LogLevel.Warning, Message = "Account {AccountId} was refused ({Outcome}): {Action} on document {DocumentId}")]
    public static partial void DocumentChangeRefused(this ILogger logger, Guid accountId, ChangeOutcome outcome, string action, Guid documentId);

    [LoggerMessage(EventId = 5257, Level = LogLevel.Information, Message = "Account {AccountId} found or created entry {EntryId} of list {Kind}")]
    public static partial void EntryFoundOrCreated(this ILogger logger, Guid accountId, Guid entryId, ClassificationKind kind);

    [LoggerMessage(EventId = 5258, Level = LogLevel.Information, Message = "Account {AccountId} renamed entry {EntryId} of list {Kind}")]
    public static partial void EntryRenamed(this ILogger logger, Guid accountId, Guid entryId, ClassificationKind kind);

    [LoggerMessage(EventId = 5259, Level = LogLevel.Information, Message = "Account {AccountId} merged {SourceCount} entries of list {Kind} into entry {EntryId}")]
    public static partial void EntriesMerged(this ILogger logger, Guid accountId, int sourceCount, ClassificationKind kind, Guid entryId);

    [LoggerMessage(EventId = 5261, Level = LogLevel.Information, Message = "Owner {AccountId} kept document {DocumentId}, whose owner was unknown on arrival")]
    public static partial void OwnerCheckConfirmed(this ILogger logger, Guid documentId, Guid accountId);

    [LoggerMessage(EventId = 5260,Level = LogLevel.Warning, Message = "Account {AccountId} was refused ({Outcome}): {Action} on list {Kind}")]
    public static partial void EntryChangeRefused(this ILogger logger, Guid accountId, ChangeOutcome outcome, string action, ClassificationKind kind);
}
