using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Editing;
using NoMorePaper.Application.Processing;
using NoMorePaper.Infrastructure.Ocr;

namespace NoMorePaper.Infrastructure.Editing;

/// <summary>
/// Reads the pages of an archive PDF with poppler (in the Worker image with OCR): <c>pdftotext</c> for the
/// text of every page; a page with fewer than <see cref="BlankCharacters"/> characters is rendered small
/// (<c>pdftoppm -r 30 -gray</c>) and counts as probably blank below <see cref="BlankInkShare"/> ink;
/// <c>pdffonts</c> on the original tells whether a page had text before OCR (a digital PDF, not a scan).
/// </summary>
public sealed class PopplerPageAnalyzer(ExternalProcess process, TimeProvider time, ILogger<PopplerPageAnalyzer> logger) : IPageAnalyzer
{
    /// <summary>A page with fewer characters may be blank; its ink decides.</summary>
    public const int BlankCharacters = 15;

    /// <summary>Below this share of dark pixels a page with hardly any text counts as blank.</summary>
    public const double BlankInkShare = 0.005;

    private const string PdfToText = "pdftotext";
    private const string PdfToPpm = "pdftoppm";
    private const string PdfFonts = "pdffonts";

    /// <summary>All steps of one document together must finish within this time.</summary>
    public static TimeSpan TimeLimit { get; } = TimeSpan.FromMinutes(3);

    /// <summary>Above this many pages the original is asked for fonts once, for all pages together.</summary>
    public const int MaxPagesAskedOneByOne = 50;

    public async Task<IReadOnlyList<PageInfo>> AnalyzeAsync(string pdfPath, string originalPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pdfPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        try
        {
            return await AnalyzeCoreAsync(pdfPath, originalPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The pages are an extra of the archive: a tool that fails, hangs or is missing must not fail OCR.
            logger.AnalysisFailed(exception.GetType().Name);
            return [];
        }
    }

    private async Task<IReadOnlyList<PageInfo>> AnalyzeCoreAsync(string pdfPath, string originalPath, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var pdf = Path.GetFullPath(pdfPath);
        var work = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(pdf)!, $".pages-{Guid.NewGuid():N}")).FullName;
        try
        {
            var textPath = Path.Combine(work, "text.txt");
            await RunAsync(PdfToText, ["-enc", "UTF-8", pdf, textPath], null, started, failOnExit: true, cancellationToken).ConfigureAwait(false);
            var texts = PopplerOutput.SplitPages(await File.ReadAllTextAsync(textPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false));
            var layers = await TextLayersAsync(Path.GetFullPath(originalPath), texts.Count, work, started, cancellationToken).ConfigureAwait(false);

            List<PageInfo> pages = [];
            for (var page = 1; page <= texts.Count; page++)
            {
                var text = texts[page - 1];
                var blank = PopplerOutput.VisibleCharacters(text) < BlankCharacters
                    && await InkShareAsync(pdf, page, work, started, cancellationToken).ConfigureAwait(false) < BlankInkShare;
                pages.Add(new PageInfo(page, PopplerOutput.Head(text, PageInfo.MaxHeadLength), blank, layers[page - 1]));
            }

            var elapsedMs = (long)time.GetElapsedTime(started).TotalMilliseconds;
            var blankPages = pages.Count(p => p.ProbablyBlank);
            var layerPages = pages.Count(p => p.HadTextLayer);
            logger.PagesAnalyzed(pages.Count, elapsedMs, blankPages, layerPages);
            return pages;
        }
        finally
        {
            DeleteWorkFolder(work);
        }
    }

    private async Task<double> InkShareAsync(string pdf, int page, string work, long started, CancellationToken cancellationToken)
    {
        var number = page.ToString(CultureInfo.InvariantCulture);
        var prefix = Path.Combine(work, "page");
        await RunAsync(PdfToPpm, ["-r", "30", "-gray", "-f", number, "-l", number, "-singlefile", pdf, prefix], null, started, failOnExit: true, cancellationToken)
            .ConfigureAwait(false);
        var image = await File.ReadAllBytesAsync(prefix + ".pgm", cancellationToken).ConfigureAwait(false);
        return PopplerOutput.DarkShare(image);
    }

    // Which pages of the original had text. An image or an unreadable PDF had none; a PDF without any
    // font had none on any page, so the pages are only asked one by one when the file has fonts at all.
    // A long document is not asked page by page (one tool start each): its answer counts for every page.
    private async Task<bool[]> TextLayersAsync(string original, int pageCount, string work, long started, CancellationToken cancellationToken)
    {
        var layers = new bool[pageCount];
        if (pageCount == 0 || !await IsPdfAsync(original, cancellationToken).ConfigureAwait(false)
            || await FontsAsync(original, null, work, started, cancellationToken).ConfigureAwait(false) != true)
        {
            return layers;
        }

        if (pageCount > MaxPagesAskedOneByOne)
        {
            Array.Fill(layers, true);
            return layers;
        }

        for (var page = 1; page <= pageCount; page++)
        {
            layers[page - 1] = await FontsAsync(original, page, work, started, cancellationToken).ConfigureAwait(false) == true;
        }

        return layers;
    }

    // null when pdffonts could not read the file (e.g. encrypted).
    private async Task<bool?> FontsAsync(string original, int? page, string work, long started, CancellationToken cancellationToken)
    {
        var listing = Path.Combine(work, "fonts.txt");
        List<string> args = [];
        if (page is { } number)
        {
            var text = number.ToString(CultureInfo.InvariantCulture);
            args.AddRange(["-f", text, "-l", text]);
        }

        args.Add(original);
        var exitCode = await RunAsync(PdfFonts, args, listing, started, failOnExit: false, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            logger.TextLayerUnknown(PdfFonts, exitCode);
            return null;
        }

        return PopplerOutput.HasFonts(await File.ReadAllTextAsync(listing, cancellationToken).ConfigureAwait(false));
    }

    private static async Task<bool> IsPdfAsync(string path, CancellationToken cancellationToken)
    {
        var header = new byte[5];
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16, FileOptions.Asynchronous);
        await using (file.ConfigureAwait(false))
        {
            var read = await file.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
            return read == header.Length && header.AsSpan().SequenceEqual("%PDF-"u8);
        }
    }

    // The tools' temporary files go to the working folder; standard output only where it is the answer.
    private async Task<int> RunAsync(
        string tool, IReadOnlyList<string> args, string? outputFile, long started, bool failOnExit, CancellationToken cancellationToken)
    {
        var remaining = TimeLimit - time.GetElapsedTime(started);
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException($"The page analysis did not finish within {TimeLimit.TotalMinutes} min.");
        }

        // The tail of standard error is not passed on: it may quote document content.
        var (exitCode, _) = outputFile is null
            ? await process.RunAsync(tool, args, remaining, cancellationToken).ConfigureAwait(false)
            : await process.RunToFileAsync(tool, args, outputFile, remaining, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0 && failOnExit)
        {
            throw new ExternalToolException(tool, exitCode, isPermanent: false);
        }

        return exitCode;
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
