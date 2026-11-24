using NoMorePaper.Application.Classification;

namespace NoMorePaper.Server.Editing;

/// <summary>
/// Log events of the scan editing routes (Server 1600–1649, docs/kb/telemetrie.md). Ids, fixed words and
/// outcomes only; never titles, file names or page texts.
/// </summary>
internal static partial class EditingLog
{
    [LoggerMessage(EventId = 1600, Level = LogLevel.Information, Message = "Edit request {Action} for document {DocumentId}: {Outcome}")]
    public static partial void EditAnswered(this ILogger logger, string action, Guid documentId, ChangeOutcome outcome);

    [LoggerMessage(EventId = 1601, Level = LogLevel.Warning, Message = "Edit request {Action} for document {DocumentId} rejected: {Reason}")]
    public static partial void EditRejected(this ILogger logger, string action, Guid documentId, string reason);
}
