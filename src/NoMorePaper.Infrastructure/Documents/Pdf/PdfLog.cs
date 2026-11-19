using Microsoft.Extensions.Logging;

namespace NoMorePaper.Infrastructure.Documents.Pdf;

/// <summary>Log events of the PDF composer (Infrastructure 3900–3949, docs/kb/telemetrie.md): kind, counts, sizes, never text.</summary>
internal static partial class PdfLog
{
    [LoggerMessage(EventId = 3900, Level = LogLevel.Information, Message = "Made a PDF from {Kind}: {Pages} pages, {SizeBytes} bytes in {ElapsedMs} ms")]
    public static partial void PdfComposed(this ILogger logger, string kind, int pages, long sizeBytes, long elapsedMs);

    [LoggerMessage(EventId = 3903, Level = LogLevel.Warning, Message = "Making a PDF from {Kind} failed ({ErrorType})")]
    public static partial void PdfFailed(this ILogger logger, string kind, string errorType);
}
