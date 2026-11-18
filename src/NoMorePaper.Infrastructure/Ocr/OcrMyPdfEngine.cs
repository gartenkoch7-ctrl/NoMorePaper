using System.Text;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Processing;

namespace NoMorePaper.Infrastructure.Ocr;

/// <summary>
/// OCR with OCRmyPDF (Tesseract <c>deu+eng</c>, output PDF/A) as decided for M1. Images become a
/// PDF first: HEIC through <c>heif-convert</c> to JPEG, JPEG and PNG through <c>img2pdf</c>, which
/// embeds them without recompressing. <c>pdftotext</c> then reads the text of the archive PDF.
/// Exit codes that say the input itself is unusable (encrypted or invalid PDF) are reported as
/// permanent, so the document is not retried.
/// </summary>
public sealed class OcrMyPdfEngine(ExternalProcess process, TimeProvider time, ILogger<OcrMyPdfEngine> logger) : IOcrEngine
{
    /// <summary>Longest text kept: the search vector of a document must stay below Postgres' 1 MB limit.</summary>
    public const int MaxTextLength = 500_000;

    public const string PdfMediaType = "application/pdf";
    public const string JpegMediaType = "image/jpeg";
    public const string PngMediaType = "image/png";
    public const string HeicMediaType = "image/heic";

    private const string OcrMyPdf = "ocrmypdf";
    private const string HeifConvert = "heif-convert";
    private const string Img2Pdf = "img2pdf";
    private const string PdfToText = "pdftotext";

    // OCRmyPDF exit codes for input that no attempt can process: 2 invalid input, 8 encrypted PDF.
    private const int InvalidInputExitCode = 2;
    private const int EncryptedPdfExitCode = 8;

    private static readonly string[] s_ocrOptions =
    [
        "--output-type", "pdfa", "--language", "deu+eng", "--rotate-pages", "--deskew", "--skip-text",

        // Two pages in parallel (M1 plan): leaves CPU for the Server and, later, the local LLM.
        "--jobs", "2",

        // Only the archive copy loses its signature; the signed original is kept unchanged next to it.
        "--invalidate-digital-signatures",
    ];

    /// <summary>All steps of one document together must finish within this time.</summary>
    public static TimeSpan TimeLimit { get; } = TimeSpan.FromMinutes(10);

    public async Task<OcrResult> CreateArchiveAsync(string inputPath, string mediaType, string outputPdfPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPdfPath);
        if (!IsSupported(mediaType))
        {
            throw new NotSupportedException("The media type cannot be processed.");
        }

        var started = time.GetTimestamp();
        var input = Path.GetFullPath(inputPath);
        var output = Path.GetFullPath(outputPdfPath);
        var work = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(output)!, $".ocr-{Guid.NewGuid():N}")).FullName;
        try
        {
            var pdf = await ToPdfAsync(input, mediaType, work, started, cancellationToken).ConfigureAwait(false);
            await RunAsync(OcrMyPdf, [.. s_ocrOptions, pdf, output], work, started, cancellationToken).ConfigureAwait(false);

            var textPath = Path.Combine(work, "text.txt");
            await RunAsync(PdfToText, ["-enc", "UTF-8", output, textPath], work, started, cancellationToken).ConfigureAwait(false);
            var text = await File.ReadAllTextAsync(textPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            return new OcrResult(CleanText(text), CountPages(text));
        }
        finally
        {
            DeleteWorkFolder(work);
        }
    }

    private static bool IsSupported(string mediaType) =>
        mediaType.Equals(PdfMediaType, StringComparison.OrdinalIgnoreCase)
        || mediaType.Equals(JpegMediaType, StringComparison.OrdinalIgnoreCase)
        || mediaType.Equals(PngMediaType, StringComparison.OrdinalIgnoreCase)
        || mediaType.Equals(HeicMediaType, StringComparison.OrdinalIgnoreCase);

    private async Task<string> ToPdfAsync(string input, string mediaType, string work, long started, CancellationToken cancellationToken)
    {
        if (mediaType.Equals(PdfMediaType, StringComparison.OrdinalIgnoreCase))
        {
            return input;
        }

        var image = input;
        if (mediaType.Equals(HeicMediaType, StringComparison.OrdinalIgnoreCase))
        {
            image = Path.Combine(work, "image.jpg");
            await RunAsync(HeifConvert, [input, image], work, started, cancellationToken).ConfigureAwait(false);

            // A file with several top-level images comes out numbered; the first one is the photo.
            var first = Path.Combine(work, "image-1.jpg");
            image = File.Exists(image) || !File.Exists(first) ? image : first;
        }

        var pdf = Path.Combine(work, "image.pdf");
        // img2pdf (0.5 and later) keeps an alpha channel as a soft mask; OCRmyPDF renders it on white.
        await RunAsync(Img2Pdf, [image, "-o", pdf], work, started, cancellationToken).ConfigureAwait(false);
        return pdf;
    }

    /// <summary>Whether an exit code of <paramref name="tool"/> means the input itself cannot be processed.</summary>
    public static bool IsPermanentFailure(string tool, int exitCode) =>
        tool == OcrMyPdf && exitCode is InvalidInputExitCode or EncryptedPdfExitCode;

    // The tools' temporary files go to the working folder: it lies on the data volume and is deleted afterwards.
    private async Task RunAsync(string tool, IReadOnlyList<string> args, string work, long started, CancellationToken cancellationToken)
    {
        var remaining = TimeLimit - time.GetElapsedTime(started);
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException($"OCR did not finish within {TimeLimit.TotalMinutes} min.");
        }

        // The tail of standard error is not passed on: it may quote document content.
        var (exitCode, _) = await process.RunAsync(tool, args, work, remaining, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new ExternalToolException(tool, exitCode, IsPermanentFailure(tool, exitCode));
        }
    }

    // pdftotext ends every page with a form feed.
    private static int CountPages(string text) => text.Count(c => c == '\f');

    private string CleanText(string text)
    {
        // Postgres text cannot hold NUL; form feeds would show up in search excerpts.
        var cleaned = text.Replace("\0", string.Empty, StringComparison.Ordinal)
            .Replace('\f', '\n')
            .Trim();
        if (cleaned.Length <= MaxTextLength)
        {
            return cleaned;
        }

        logger.TextCut(MaxTextLength);
        var length = char.IsHighSurrogate(cleaned[MaxTextLength - 1]) ? MaxTextLength - 1 : MaxTextLength;
        return cleaned[..length];
    }

    private void DeleteWorkFolder(string work)
    {
        try
        {
            Directory.Delete(work, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.CleanupFailed(exception.GetType().Name);
        }
    }
}
