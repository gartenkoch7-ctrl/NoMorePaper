using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Processing;

/// <summary>
/// Log events of the processing queue (Infrastructure 3976–3977, docs/kb/telemetrie.md). Document ids only.
/// </summary>
internal static partial class QueueLog
{
    [LoggerMessage(EventId = 3976, Level = LogLevel.Information, Message = "The waiting recognition of document {DocumentId} was taken over by an edit; it follows the edit")]
    public static partial void RecognitionTakenOver(this ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 3977, Level = LogLevel.Information, Message = "An edit of document {DocumentId} was refused: another job of it runs or waits")]
    public static partial void EditRefusedJobPending(this ILogger logger, Guid documentId);
}
