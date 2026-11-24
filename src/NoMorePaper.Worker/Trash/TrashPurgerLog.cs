namespace NoMorePaper.Worker.Trash;

/// <summary>
/// Log events of the daily trash purge (Worker 2500–2599, docs/kb/telemetrie.md). Counts, durations and
/// exception types only.
/// </summary>
internal static partial class TrashPurgerLog
{
    [LoggerMessage(EventId = 2500, Level = LogLevel.Information, Message = "Trash purge started, running every {IntervalHours} h")]
    public static partial void PurgerStarted(this ILogger logger, int intervalHours);

    [LoggerMessage(EventId = 2501, Level = LogLevel.Information, Message = "Trash purge ran: {Count} entries removed for good after {ElapsedMs} ms")]
    public static partial void PurgeRan(this ILogger logger, int count, long elapsedMs);

    [LoggerMessage(EventId = 2502, Level = LogLevel.Error, Message = "Trash purge failed ({ExceptionType}); it runs again at its next time")]
    public static partial void PurgeFailed(this ILogger logger, string exceptionType);
}
