using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Editing;

/// <summary>
/// Log events of the page tools (Infrastructure 3950–3969, docs/kb/telemetrie.md): qpdf and the page
/// analysis with poppler. Counts, durations, tool names and exit codes only: tool output may quote documents.
/// </summary>
internal static partial class EditingLog
{
    [LoggerMessage(EventId = 3950, Level = LogLevel.Information, Message = "qpdf composed a PDF of {Pages} pages in {ElapsedMs} ms")]
    public static partial void Composed(this ILogger logger, int pages, long elapsedMs);

    [LoggerMessage(EventId = 3951, Level = LogLevel.Information, Message = "Analyzed {Pages} pages in {ElapsedMs} ms; {BlankPages} probably blank, {LayerPages} with text in the original")]
    public static partial void PagesAnalyzed(this ILogger logger, int pages, long elapsedMs, int blankPages, int layerPages);

    [LoggerMessage(EventId = 3952, Level = LogLevel.Information, Message = "{Tool} exited with code {ExitCode}; the original counts as having no text layer")]
    public static partial void TextLayerUnknown(this ILogger logger, string tool, int exitCode);

    [LoggerMessage(EventId = 3953, Level = LogLevel.Warning, Message = "Could not delete a working folder of the page tools ({ErrorType}); only space is lost")]
    public static partial void CleanupFailed(this ILogger logger, string errorType);

    [LoggerMessage(EventId = 3954, Level = LogLevel.Warning, Message = "Could not analyze the pages ({ErrorType}); the archive is stored without page information")]
    public static partial void AnalysisFailed(this ILogger logger, string errorType);
}
