using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Documents;
using PdfSharp.Fonts;

namespace NoMorePaper.Infrastructure.Documents.Pdf;

/// <summary>
/// <see cref="IPdfComposer"/> with PDFsharp (MIT, ADR-0024). Only hands the work to the writers; the
/// PDF is built in memory and copied to the target, so a failure never leaves half a file there.
/// </summary>
/// <remarks>Singleton: the font resolver is a process-wide setting of PDFsharp and is installed once.</remarks>
public sealed class PdfSharpComposer : IPdfComposer
{
    // PDFsharp keeps its font resolver in a static property that may be set only once per process.
    private static readonly Lock s_fontsLock = new();

    private readonly TimeProvider _time;
    private readonly ILogger<PdfSharpComposer> _logger;
    private readonly ImagePdfWriter _images = new();
    private readonly TextPdfWriter _text = new();

    public PdfSharpComposer(TimeProvider time, ILogger<PdfSharpComposer> logger)
    {
        _time = time;
        _logger = logger;
        lock (s_fontsLock)
        {
            GlobalFontSettings.FontResolver ??= new EmbeddedFontResolver();
        }
    }

    public Task ComposeImagesAsync(IReadOnlyList<Stream> jpegs, Stream target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(jpegs);
        ArgumentOutOfRangeException.ThrowIfZero(jpegs.Count);
        return ComposeAsync("images", buffer => _images.Write(jpegs, buffer), target, cancellationToken);
    }

    public Task ComposeTextAsync(TextPage page, Stream target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);
        return ComposeAsync("text", buffer => _text.Write(page, buffer), target, cancellationToken);
    }

    private async Task ComposeAsync(string kind, Func<Stream, int> write, Stream target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        var started = _time.GetTimestamp();
        using var buffer = new MemoryStream();
        int pages;
        try
        {
            pages = write(buffer);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // PDFsharp reports unreadable images in many ways; the type is enough, the message may quote data.
            _logger.PdfFailed(kind, exception.GetType().Name);
            throw new InvalidDataException($"The {kind} PDF could not be made.", exception);
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        var elapsedMs = (long)_time.GetElapsedTime(started).TotalMilliseconds;
        _logger.PdfComposed(kind, pages, buffer.Length, elapsedMs);
    }
}
