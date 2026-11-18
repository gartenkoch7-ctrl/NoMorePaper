namespace NoMorePaper.Worker.Heartbeat;

/// <summary>Log events of the worker heartbeat (Worker range 2000–2999, docs/kb/telemetrie.md).</summary>
internal static partial class HeartbeatLog
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Heartbeat started, every {IntervalSeconds} s")]
    public static partial void HeartbeatStarted(this ILogger logger, int intervalSeconds);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "Heartbeat could not be written; retrying on the next tick")]
    public static partial void HeartbeatFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Information, Message = "Heartbeat recovered")]
    public static partial void HeartbeatRecovered(this ILogger logger);
}
