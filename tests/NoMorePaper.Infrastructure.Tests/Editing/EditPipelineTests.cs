using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Infrastructure.Documents.Pdf;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Ocr;
using NoMorePaper.Infrastructure.Tests.Ocr;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Editing;

/// <summary>
/// The tools of the edit stage together, as the Worker uses them (M4b P1): pages of an OCR archive turned,
/// reordered, deleted and replaced with qpdf, OCR again without corrections, and split parts that keep a text
/// layer on every page, so the stack separation leaves them alone. Linux only, like the other OCR tests.
/// </summary>
public sealed class EditPipelineTests(OcrSamples samples) : IClassFixture<OcrSamples>
{
    private readonly ExternalProcess _process = new(TimeProvider.System, NullLogger<ExternalProcess>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnEditOfTheArchive_KeepsTheTextOfTurnedPages_AndRecognizesAReplacedOne()
    {
        OcrSamples.SkipUnlessToolsInstalled();
        var work = samples.NewWorkFolder();
        var archive = await OcrAsync(samples.ScannedPdf, "application/pdf", Path.Combine(work, "archive.pdf"), correctPages: true);

        // Page 2 first, turned right; page 1 deleted; a cropped page (the photo of page 1) last.
        var replaced = Path.Combine(work, "replaced-1.pdf");
        await using (var target = File.Create(replaced))
        await using (var jpeg = File.OpenRead(samples.Jpeg))
        {
            await new PdfSharpComposer(TimeProvider.System, NullLogger<PdfSharpComposer>.Instance).ComposeImagesAsync([jpeg], target, Ct);
        }

        var edited = Path.Combine(work, "edited.pdf");
        await Qpdf().ComposeAsync([new(archive, 2, PageRotation.Right), new(replaced, 1, PageRotation.None)], edited, Ct);
        var result = await Engine().CreateArchiveAsync(edited, "application/pdf", Path.Combine(work, "edited-archive.pdf"), correctPages: false, Ct);

        Assert.Equal(2, result.PageCount);
        Assert.Equal(2, await Qpdf().CountPagesAsync(Path.Combine(work, "edited-archive.pdf"), Ct));
        Assert.Contains("Zahlungsfrist", result.Text, StringComparison.Ordinal);
        Assert.Contains(OcrSamples.GermanWord, result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SplitParts_FromTheArchive_HaveATextLayerOnEveryPage()
    {
        OcrSamples.SkipUnlessToolsInstalled();
        var work = samples.NewWorkFolder();
        var archive = await OcrAsync(samples.ScannedPdf, "application/pdf", Path.Combine(work, "archive.pdf"), correctPages: true);
        var part = Path.Combine(work, "part-1.pdf");

        await Qpdf().ComposeAsync([new(archive, 1, PageRotation.None), new(archive, 2, PageRotation.Half)], part, Ct);
        var partArchive = await OcrAsync(part, "application/pdf", Path.Combine(work, "part-archive.pdf"), correctPages: true);
        var pages = await new PopplerPageAnalyzer(_process, TimeProvider.System, NullLogger<PopplerPageAnalyzer>.Instance)
            .AnalyzeAsync(partArchive, part, Ct);

        Assert.Equal(2, pages.Count);
        Assert.All(pages, page => Assert.True(page.HadTextLayer));
    }

    private async Task<string> OcrAsync(string input, string mediaType, string output, bool correctPages)
    {
        await Engine().CreateArchiveAsync(input, mediaType, output, correctPages, Ct);
        return output;
    }

    private OcrMyPdfEngine Engine() => new(_process, TimeProvider.System, NullLogger<OcrMyPdfEngine>.Instance);

    private QpdfTool Qpdf() => new(_process, TimeProvider.System, NullLogger<QpdfTool>.Instance);
}
