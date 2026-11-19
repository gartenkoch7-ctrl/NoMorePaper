using Microsoft.Extensions.Logging;

namespace NoMorePaper.Web.Client.Camera;

/// <summary>
/// Log events of the camera page (EventId range 4600–4699, see docs/kb/telemetrie.md). Only sizes,
/// counts, durations and fixed error codes; never image data.
/// </summary>
internal static partial class CameraLog
{
    [LoggerMessage(EventId = 4600, EventName = "CameraStarted", Level = LogLevel.Information,
        Message = "Camera started at {Width}x{Height}, edge detection ready after {ElapsedMs} ms")]
    public static partial void CameraStarted(this ILogger logger, int width, int height, int elapsedMs);

    [LoggerMessage(EventId = 4601, EventName = "CameraFailed", Level = LogLevel.Warning,
        Message = "Camera error: {Code}")]
    public static partial void CameraFailed(this ILogger logger, string code);

    [LoggerMessage(EventId = 4602, EventName = "PageCaptured", Level = LogLevel.Information,
        Message = "Page {Index} captured in {ElapsedMs} ms")]
    public static partial void PageCaptured(this ILogger logger, int index, int elapsedMs);

    [LoggerMessage(EventId = 4603, EventName = "PagesUploaded", Level = LogLevel.Information,
        Message = "Camera document with {Pages} pages accepted by the server")]
    public static partial void PagesUploaded(this ILogger logger, int pages);

    [LoggerMessage(EventId = 4604, EventName = "CameraStopped", Level = LogLevel.Information,
        Message = "Camera stopped after {Frames} analysed frames, {ElapsedMs} ms per analysis on average")]
    public static partial void CameraStopped(this ILogger logger, int frames, int elapsedMs);

    [LoggerMessage(EventId = 4605, EventName = "CameraPreferenceUnavailable", Level = LogLevel.Warning,
        Message = "Browser storage for the camera tones is unavailable")]
    public static partial void CameraPreferenceUnavailable(this ILogger logger);
}
