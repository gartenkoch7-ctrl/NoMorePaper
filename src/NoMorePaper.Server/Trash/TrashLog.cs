using NoMorePaper.Application.Classification;

namespace NoMorePaper.Server.Trash;

/// <summary>
/// Log events of the trash API (Server 1700–1749, docs/kb/telemetrie.md). Ids, fixed words, outcomes and
/// counts only: never titles or file names.
/// </summary>
internal static partial class TrashLog
{
    [LoggerMessage(EventId = 1700, Level = LogLevel.Debug, Message = "Trash listed with {Count} entries")]
    public static partial void TrashListed(this ILogger logger, int count);

    [LoggerMessage(EventId = 1701, Level = LogLevel.Information, Message = "Trash request {Action} on {Id} answered {Outcome}")]
    public static partial void TrashAnswered(this ILogger logger, string action, Guid id, ChangeOutcome outcome);

    [LoggerMessage(EventId = 1702, Level = LogLevel.Information, Message = "Trash emptied: {Count} entries removed for good")]
    public static partial void TrashEmptied(this ILogger logger, int count);
}
