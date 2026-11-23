using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Editing;

/// <summary>
/// Log events of the page editor (EventId range 4300–4349, see docs/kb/telemetrie.md).
/// Only counts and HTTP status codes are logged, never titles, page texts or file names.
/// </summary>
internal static partial class PageEditorLog
{
    [LoggerMessage(EventId = 4300, EventName = "PageEditorLoaded", Level = LogLevel.Information,
        Message = "Page editor loaded a document with {Pages} pages")]
    public static partial void PageEditorLoaded(this ILogger logger, int pages);

    [LoggerMessage(EventId = 4301, EventName = "PageEditorSaved", Level = LogLevel.Information,
        Message = "Page editor saved a plan with {Steps} pages in {Splits} parts")]
    public static partial void PageEditorSaved(this ILogger logger, int steps, int splits);

    [LoggerMessage(EventId = 4302, EventName = "PageEditorAppendSent", Level = LogLevel.Information,
        Message = "Page editor asked to append a document")]
    public static partial void PageEditorAppendSent(this ILogger logger);

    [LoggerMessage(EventId = 4303, EventName = "PageEditorFailed", Level = LogLevel.Warning,
        Message = "Page editor call failed: {Status}")]
    public static partial void PageEditorFailed(this ILogger logger, string status);

    [LoggerMessage(EventId = 4310, EventName = "PageCropDetected", Level = LogLevel.Information,
        Message = "Page editor drew a page to crop and looked for the sheet in {ElapsedMs} ms, sheet found: {Found}")]
    public static partial void PageCropDetected(this ILogger logger, int elapsedMs, bool found);

    [LoggerMessage(EventId = 4311, EventName = "PageCropFailed", Level = LogLevel.Warning,
        Message = "Page editor could not crop a page: {Reason}")]
    public static partial void PageCropFailed(this ILogger logger, string reason);
}
