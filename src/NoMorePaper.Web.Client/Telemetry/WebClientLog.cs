using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Telemetry;

/// <summary>
/// Log events of the web client (EventId range 4000–4999, see docs/kb/telemetrie.md).
/// Only language codes are logged, never user content.
/// </summary>
internal static partial class WebClientLog
{
    [LoggerMessage(EventId = 4000, EventName = "CultureApplied", Level = LogLevel.Information,
        Message = "UI culture {Culture} applied")]
    public static partial void CultureApplied(this ILogger logger, string culture);

    [LoggerMessage(EventId = 4001, EventName = "StoredCultureUnsupported", Level = LogLevel.Warning,
        Message = "Stored UI culture is not supported, falling back to {Culture}")]
    public static partial void StoredCultureUnsupported(this ILogger logger, string culture);

    [LoggerMessage(EventId = 4002, EventName = "CulturePreferenceUnavailable", Level = LogLevel.Warning,
        Message = "Browser storage for the UI culture is unavailable, using {Culture} for this session")]
    public static partial void CulturePreferenceUnavailable(this ILogger logger, string culture);

    [LoggerMessage(EventId = 4003, EventName = "CultureChangeRequested", Level = LogLevel.Information,
        Message = "UI culture change to {Culture} requested, reloading")]
    public static partial void CultureChangeRequested(this ILogger logger, string culture);
}
