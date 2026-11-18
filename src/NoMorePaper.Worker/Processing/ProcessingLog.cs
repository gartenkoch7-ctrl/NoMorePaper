namespace NoMorePaper.Worker.Processing;

/// <summary>
/// Log events of the processing loop and its working folder (Worker 2100–2199, docs/kb/telemetrie.md);
/// the events of the attempts themselves belong to <c>Application/Processing/ProcessingLog</c>.
/// Only counts, intervals and exception types; never paths.
/// </summary>
internal static partial class ProcessingLog
{
    [LoggerMessage(EventId = 2100, Level = LogLevel.Information, Message = "Document processing started, polling every {IntervalSeconds} s")]
    public static partial void ProcessingStarted(this ILogger logger, int intervalSeconds);

    [LoggerMessage(EventId = 2112, Level = LogLevel.Information, Message = "Removed {Count} leftovers older than a day from the working folder")]
    public static partial void LeftoversRemoved(this ILogger logger, int count);

    [LoggerMessage(EventId = 2113, Level = LogLevel.Warning, Message = "Could not remove a leftover from the working folder ({ErrorType}); only space is lost")]
    public static partial void LeftoverNotRemoved(this ILogger logger, string errorType);
}
