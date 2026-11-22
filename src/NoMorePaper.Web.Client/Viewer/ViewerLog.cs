using Microsoft.Extensions.Logging;
using NoMorePaper.Contracts.Documents;

namespace NoMorePaper.Web.Client.Viewer;

/// <summary>
/// Log events of the viewer and the thumbnails (EventId range 4200–4299, see docs/kb/telemetrie.md).
/// Only counts, durations, renderer kinds, states and technical error names are logged, never
/// titles, file names or document content.
/// </summary>
internal static partial class ViewerLog
{
    [LoggerMessage(EventId = 4200, EventName = "ViewerOpened", Level = LogLevel.Information,
        Message = "Viewer opened a document with {PageCount} pages using {Renderer}")]
    public static partial void ViewerOpened(this ILogger logger, int pageCount, string renderer);

    [LoggerMessage(EventId = 4201, EventName = "WebGlFallback", Level = LogLevel.Warning,
        Message = "WebGL2 is not usable ({Reason}); the viewer falls back to the CSS filter")]
    public static partial void WebGlFallback(this ILogger logger, string reason);

    [LoggerMessage(EventId = 4202, EventName = "PageRendered", Level = LogLevel.Debug,
        Message = "Visible part of a page rendered in {DurationMs} ms")]
    public static partial void PageRendered(this ILogger logger, int durationMs);

    [LoggerMessage(EventId = 4203, EventName = "ViewerOpenFailed", Level = LogLevel.Warning,
        Message = "Viewer could not open the document: {ErrorType}")]
    public static partial void ViewerOpenFailed(this ILogger logger, string errorType);

    [LoggerMessage(EventId = 4204, EventName = "ViewerRenderFailed", Level = LogLevel.Warning,
        Message = "Viewer could not render a page: {ErrorType}")]
    public static partial void ViewerRenderFailed(this ILogger logger, string errorType);

    [LoggerMessage(EventId = 4205, EventName = "DocumentNotViewable", Level = LogLevel.Information,
        Message = "Document has no PDF to show yet (state {State})")]
    public static partial void DocumentNotViewable(this ILogger logger, DocumentProcessingState state);

    [LoggerMessage(EventId = 4206, EventName = "PageWithoutText", Level = LogLevel.Information,
        Message = "Shown page has no text layer")]
    public static partial void PageWithoutText(this ILogger logger);

    [LoggerMessage(EventId = 4207, EventName = "OriginalColorsChanged", Level = LogLevel.Information,
        Message = "Original colours switched {State}")]
    public static partial void OriginalColorsChanged(this ILogger logger, string state);

    [LoggerMessage(EventId = 4208, EventName = "ViewerPreferenceUnavailable", Level = LogLevel.Warning,
        Message = "Browser storage for the viewer setting is unavailable, using the default for this session")]
    public static partial void ViewerPreferenceUnavailable(this ILogger logger);

    [LoggerMessage(EventId = 4209, EventName = "ThumbnailFailed", Level = LogLevel.Warning,
        Message = "Thumbnail could not be rendered: {ErrorType}")]
    public static partial void ThumbnailFailed(this ILogger logger, string errorType);

    [LoggerMessage(EventId = 4210, EventName = "WebGlContextLost", Level = LogLevel.Warning,
        Message = "The browser took the viewer's WebGL context away; waiting for it to come back")]
    public static partial void WebGlContextLost(this ILogger logger);

    [LoggerMessage(EventId = 4211, EventName = "WebGlContextRestored", Level = LogLevel.Information,
        Message = "The viewer's WebGL context is back and the page was drawn again")]
    public static partial void WebGlContextRestored(this ILogger logger);

    [LoggerMessage(EventId = 4212, EventName = "ViewerTabSelected", Level = LogLevel.Debug,
        Message = "Document page: tab {Tab} selected")]
    public static partial void ViewerTabSelected(this ILogger logger, string tab);

    [LoggerMessage(EventId = 4213, EventName = "RecognizedTextLoaded", Level = LogLevel.Information,
        Message = "Recognized text loaded ({Length} characters)")]
    public static partial void RecognizedTextLoaded(this ILogger logger, int length);

    [LoggerMessage(EventId = 4214, EventName = "RecognizedTextCopied", Level = LogLevel.Information,
        Message = "Recognized text copied to the clipboard")]
    public static partial void RecognizedTextCopied(this ILogger logger);

    [LoggerMessage(EventId = 4215, EventName = "RecognizedTextCopyFailed", Level = LogLevel.Warning,
        Message = "Recognized text could not be copied: {ExceptionType}")]
    public static partial void RecognizedTextCopyFailed(this ILogger logger, string exceptionType);

    [LoggerMessage(EventId = 4216, EventName = "FullscreenChanged", Level = LogLevel.Information,
        Message = "Fullscreen of the viewer switched {State}")]
    public static partial void FullscreenChanged(this ILogger logger, string state);

    [LoggerMessage(EventId = 4217, EventName = "FullscreenFailed", Level = LogLevel.Warning,
        Message = "Fullscreen of the viewer failed: {ErrorType}")]
    public static partial void FullscreenFailed(this ILogger logger, string errorType);
}
