namespace NoMorePaper.Server.Startup;

/// <summary>Log events of the Server startup (Server range 1000–1999, docs/kb/telemetrie.md).</summary>
internal static partial class StartupLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Server startup tasks completed in {ElapsedMs} ms")]
    public static partial void StartupCompleted(this ILogger logger, long elapsedMs);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Critical, Message = "Server startup failed; the host stops")]
    public static partial void StartupFailed(this ILogger logger, Exception exception);
}
