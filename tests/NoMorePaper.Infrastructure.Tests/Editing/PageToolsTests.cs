using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Processing;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Infrastructure.Editing;
using NoMorePaper.Infrastructure.Ocr;
using NoMorePaper.Infrastructure.Tests.Ocr;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Editing;

/// <summary>
/// qpdf and the page analysis with the real tools; skipped where they are not installed (e.g. a Windows
/// dev box). The samples come from <see cref="OcrSamples"/>: a two-page PDF with text and the same scanned.
/// </summary>
public sealed class PageToolsTests(OcrSamples samples) : IClassFixture<OcrSamples>
{
    private static readonly bool s_qpdfInstalled = OnPath("qpdf");

    private readonly ExternalProcess _process = new(TimeProvider.System, NullLogger<ExternalProcess>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Qpdf_ReordersTurnsAndJoinsPages_AndCountsThem()
    {
        SkipUnlessToolsInstalled();
        var work = samples.NewWorkFolder();
        var output = Path.Combine(work, "out.pdf");
        var qpdf = new QpdfTool(_process, TimeProvider.System, NullLogger<QpdfTool>.Instance);

        await qpdf.ComposeAsync(
            [new(samples.TextPdf, 2, PageRotation.None), new(samples.TextPdf, 1, PageRotation.Right), new(samples.ScannedPdf, 1, PageRotation.Half)],
            output,
            Ct);

        Assert.Equal(3, await qpdf.CountPagesAsync(output, Ct));
        Assert.Equal(2, await qpdf.CountPagesAsync(samples.TextPdf, Ct));
        var pages = await Analyzer().AnalyzeAsync(output, output, Ct);
        Assert.StartsWith("Zahlungsfrist", pages[0].Head, StringComparison.Ordinal);
        Assert.Contains("Rechnung", pages[1].Head, StringComparison.Ordinal);
        Assert.Equal([true, true, false], pages.Select(page => page.HadTextLayer));
        Assert.Empty(Directory.GetFiles(work, ".npages-*"));

        // A retried split must make the same part again (same hash), so the same pages give the same bytes.
        var again = Path.Combine(work, "again.pdf");
        await qpdf.ComposeAsync(
            [new(samples.TextPdf, 2, PageRotation.None), new(samples.TextPdf, 1, PageRotation.Right), new(samples.ScannedPdf, 1, PageRotation.Half)],
            again,
            Ct);
        Assert.Equal(await File.ReadAllBytesAsync(output, Ct), await File.ReadAllBytesAsync(again, Ct));
    }

    [Fact]
    public async Task Qpdf_ReportsAFailure_ForAFileThatIsNoPdf()
    {
        SkipUnlessToolsInstalled();
        var work = samples.NewWorkFolder();
        var notPdf = Path.Combine(work, "kaputt.pdf");
        await File.WriteAllTextAsync(notPdf, "kein PDF", Ct);
        var qpdf = new QpdfTool(_process, TimeProvider.System, NullLogger<QpdfTool>.Instance);

        var failure = await Assert.ThrowsAsync<ExternalToolException>(() => qpdf.CountPagesAsync(notPdf, Ct));
        Assert.Equal("qpdf", failure.Tool);
    }

    [Fact]
    public async Task Analyzer_ReadsTheStartOfEachPage_AndWhetherTheOriginalHadText()
    {
        SkipUnlessToolsInstalled();

        var withText = await Analyzer().AnalyzeAsync(samples.TextPdf, samples.TextPdf, Ct);
        var scanned = await Analyzer().AnalyzeAsync(samples.TextPdf, samples.ScannedPdf, Ct);
        var photo = await Analyzer().AnalyzeAsync(samples.TextPdf, samples.Png, Ct);

        Assert.Equal([1, 2], withText.Select(page => page.Page));
        Assert.Equal("Rechnung Stromanbieter Januar Invoice for electricity Seite 1", withText[0].Head);
        Assert.All(withText, page => Assert.False(page.ProbablyBlank));
        Assert.All(withText, page => Assert.True(page.HadTextLayer));
        Assert.All(scanned, page => Assert.False(page.HadTextLayer));
        Assert.All(photo, page => Assert.False(page.HadTextLayer));
    }

    [Fact]
    public async Task Analyzer_FindsABlankPage()
    {
        SkipUnlessToolsInstalled();
        var work = samples.NewWorkFolder();
        var source = Path.Combine(work, "blank.ps");
        var pdf = Path.Combine(work, "blank.pdf");
        await File.WriteAllTextAsync(source, "%!PS\n/Helvetica findfont 30 scalefont setfont\n72 700 moveto (Brief) show\nshowpage\nshowpage\n", Ct);
        var (exitCode, _) = await _process.RunAsync(
            "gs", ["-q", "-dNOPAUSE", "-dBATCH", "-dSAFER", "-sDEVICE=pdfwrite", "-o", pdf, source], TimeSpan.FromMinutes(1), Ct);
        Assert.Equal(0, exitCode);

        var pages = await Analyzer().AnalyzeAsync(pdf, pdf, Ct);

        Assert.Equal([false, true], pages.Select(page => page.ProbablyBlank));
        Assert.Equal(string.Empty, pages[1].Head);
        Assert.Empty(Directory.GetDirectories(work, ".pages-*"));
    }

    [Fact]
    public async Task Analyzer_ReturnsNoPages_AndWarns_WhenAToolFails()
    {
        // Runs everywhere: without poppler the tool cannot start, with it the file is no PDF.
        var work = Directory.CreateTempSubdirectory("nomorepaper-pages-");
        try
        {
            var notPdf = Path.Combine(work.FullName, "kaputt.pdf");
            await File.WriteAllTextAsync(notPdf, "kein PDF", Ct);
            var log = new RecordingLogger<PopplerPageAnalyzer>();

            var pages = await new PopplerPageAnalyzer(_process, TimeProvider.System, log).AnalyzeAsync(notPdf, notPdf, Ct);

            Assert.Empty(pages);
            Assert.Contains(3954, log.EventIds);
            Assert.Equal(["kaputt.pdf"], work.GetFileSystemInfos().Select(entry => entry.Name));
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private PopplerPageAnalyzer Analyzer() => new(_process, TimeProvider.System, NullLogger<PopplerPageAnalyzer>.Instance);

    private static void SkipUnlessToolsInstalled()
    {
        OcrSamples.SkipUnlessToolsInstalled();
        Assert.SkipUnless(s_qpdfInstalled, "qpdf is not installed here; the page tool tests run in CI and in the Worker image.");
    }

    private static bool OnPath(string tool) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => File.Exists(Path.Combine(folder, tool)) || File.Exists(Path.Combine(folder, tool + ".exe")));
}
