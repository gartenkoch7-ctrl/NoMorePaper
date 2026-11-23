using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Start;

/// <summary>
/// Log events of the start page (EventId range 4800–4899, see docs/kb/telemetrie.md). Only part names and counts,
/// never titles or names. The review counter logs for itself (4514).
/// </summary>
internal static partial class StartLog
{
    /// <param name="part">A fixed word: <c>recent</c> or <c>inputs</c>.</param>
    [LoggerMessage(EventId = 4800, EventName = "StartPartLoaded", Level = LogLevel.Debug,
        Message = "Start page part {Part} loaded with {Count} entries")]
    public static partial void StartPartLoaded(this ILogger logger, string part, int count);

    [LoggerMessage(EventId = 4801, EventName = "StartPartFailed", Level = LogLevel.Warning,
        Message = "Start page part {Part} could not be loaded")]
    public static partial void StartPartFailed(this ILogger logger, string part);
}
