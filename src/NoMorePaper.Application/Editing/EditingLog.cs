using Microsoft.Extensions.Logging;
using NoMorePaper.Domain.Documents;

namespace NoMorePaper.Application.Editing;

/// <summary>
/// Log events of scan editing (Application 5500–5515, docs/kb/telemetrie.md): requests, the edit stage
/// in the Worker and the page backlog. Ids, kinds, counts, durations and technical reasons only; never
/// titles, file names or page texts.
/// </summary>
internal static partial class EditingLog
{
    [LoggerMessage(EventId = 5500, Level = LogLevel.Information, Message = "Account {AccountId} queued an edit ({Kind}) of document {DocumentId}")]
    public static partial void EditRequested(this ILogger logger, Guid accountId, EditKind kind, Guid documentId);

    [LoggerMessage(EventId = 5501, Level = LogLevel.Information, Message = "An edit ({Kind}) of document {DocumentId} was refused: another job of it runs or waits")]
    public static partial void EditRefusedBusy(this ILogger logger, EditKind kind, Guid documentId);

    [LoggerMessage(EventId = 5502, Level = LogLevel.Warning, Message = "An edit ({Kind}) of document {DocumentId} was refused: {Reason}")]
    public static partial void EditInvalid(this ILogger logger, EditKind kind, Guid documentId, string reason);

    [LoggerMessage(EventId = 5503, Level = LogLevel.Information, Message = "Owner {AccountId} chose to keep {Policy} for document {DocumentId}")]
    public static partial void VersionsChosen(this ILogger logger, Guid accountId, VersionPolicy policy, Guid documentId);

    [LoggerMessage(EventId = 5504, Level = LogLevel.Warning, Message = "The {Kind} of document {DocumentId} is in the trash as entry {TrashItemId}, but its file could not be moved there ({ErrorType})")]
    public static partial void TrashMoveFailed(this ILogger logger, DocumentFileKind kind, Guid documentId, Guid trashItemId, string errorType);

    [LoggerMessage(EventId = 5505, Level = LogLevel.Information, Message = "Editing document {DocumentId} ({Kind}), attempt {Attempt}")]
    public static partial void EditStarted(this ILogger logger, Guid documentId, EditKind kind, int attempt);

    [LoggerMessage(EventId = 5506, Level = LogLevel.Information, Message = "Edit ({Kind}) of document {DocumentId} stored after {ElapsedMs} ms ({Pages} pages)")]
    public static partial void EditStored(this ILogger logger, EditKind kind, Guid documentId, long elapsedMs, int pages);

    [LoggerMessage(EventId = 5507, Level = LogLevel.Warning, Message = "Attempt {Attempt} of the edit of document {DocumentId} failed ({Reason}); next attempt at {NextAttemptAt:O}")]
    public static partial void EditAttemptFailed(this ILogger logger, int attempt, Guid documentId, string reason, DateTimeOffset nextAttemptAt);

    [LoggerMessage(EventId = 5508, Level = LogLevel.Error, Message = "The edit of document {DocumentId} was given up after attempt {Attempt} ({Reason}); the document keeps its previous version")]
    public static partial void EditGivenUp(this ILogger logger, Guid documentId, int attempt, string reason);

    [LoggerMessage(EventId = 5509, Level = LogLevel.Information, Message = "Edit of document {DocumentId} finished: its files are in place")]
    public static partial void EditFinished(this ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 5510, Level = LogLevel.Information, Message = "Split of document {DocumentId}: part {Part} is document {PartId}")]
    public static partial void SplitPartCreated(this ILogger logger, Guid documentId, int part, Guid partId);

    [LoggerMessage(EventId = 5511, Level = LogLevel.Information, Message = "Page information of document {DocumentId} stored ({Pages} pages)")]
    public static partial void PagesAnalyzed(this ILogger logger, Guid documentId, int pages);

    [LoggerMessage(EventId = 5512, Level = LogLevel.Information, Message = "Page backlog: {Count} documents queued for their page information")]
    public static partial void PageBacklogQueued(this ILogger logger, int count);

    [LoggerMessage(EventId = 5513, Level = LogLevel.Warning, Message = "Could not remove the files of an edit of document {DocumentId} ({ErrorType}); only space is lost")]
    public static partial void EditFilesNotDiscarded(this ILogger logger, Guid documentId, string errorType);

    [LoggerMessage(EventId = 5514, Level = LogLevel.Information, Message = "The waiting page analysis of document {DocumentId} was replaced by an edit")]
    public static partial void AnalysisReplaced(this ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 5515, Level = LogLevel.Information, Message = "Owner {AccountId} chose to keep {Policy} for document {DocumentId}; a new archive is queued")]
    public static partial void RebuildQueued(this ILogger logger, Guid accountId, VersionPolicy policy, Guid documentId);
}
