using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Trash;

/// <summary>
/// Log events of the trash page and the "In den Papierkorb" button (EventId range 4800–4899, see
/// docs/kb/telemetrie.md). Only counts and HTTP status codes are logged, never titles.
/// </summary>
internal static partial class TrashLog
{
    [LoggerMessage(EventId = 4800, EventName = "TrashLoaded", Level = LogLevel.Debug,
        Message = "Trash loaded: {Count} entries")]
    public static partial void TrashLoaded(this ILogger logger, int count);

    [LoggerMessage(EventId = 4801, EventName = "TrashItemRestored", Level = LogLevel.Information,
        Message = "A trash entry was restored")]
    public static partial void TrashItemRestored(this ILogger logger);

    [LoggerMessage(EventId = 4802, EventName = "TrashItemPurged", Level = LogLevel.Information,
        Message = "A trash entry was deleted for good")]
    public static partial void TrashItemPurged(this ILogger logger);

    [LoggerMessage(EventId = 4803, EventName = "TrashEmptied", Level = LogLevel.Information,
        Message = "The trash was emptied: {Count} entries")]
    public static partial void TrashEmptied(this ILogger logger, int count);

    [LoggerMessage(EventId = 4804, EventName = "DocumentMovedToTrash", Level = LogLevel.Information,
        Message = "A document was put into the trash from the viewer")]
    public static partial void DocumentMovedToTrash(this ILogger logger);

    // The client only knows whether the server refused with 409; any other failure is logged as 0.
    [LoggerMessage(EventId = 4805, EventName = "TrashActionFailed", Level = LogLevel.Warning,
        Message = "A trash action failed with HTTP status {Status}")]
    public static partial void TrashActionFailed(this ILogger logger, int status);
}
