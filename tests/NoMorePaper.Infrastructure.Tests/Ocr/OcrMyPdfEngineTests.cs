using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Processing;
using NoMorePaper.Infrastructure.Ocr;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Ocr;

/// <summary>Real OCR with OCRmyPDF and Tesseract; skipped where the tools are not installed (e.g. a Windows dev box).</summary>
public sealed class OcrMyPdfEngineTests(OcrSamples samples) : IClassFixture<OcrSamples>
{
    private readonly ExternalProcess _process = new(TimeProvider.System, NullLogger<ExternalProcess>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateArchive_RecognizesTheText_OfAScannedPdfWithoutText()
    {
        OcrSamples.SkipUnlessToolsInstalled();

        var (result, output) = await CreateArchiveAsync(samples.ScannedPdf, OcrMyPdfEngine.PdfMediaType);

        Assert.Equal(2, result.PageCount);
        Assert.Contains(OcrSamples.GermanWord, result.Text, StringComparison.Ordinal);
        Assert.Contains(OcrSamples.EnglishWord, result.Text, StringComparison.Ordinal);
        Assert.Contains("Zahlungsfrist", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain('\f', result.Text);
        await AssertIsPdfAAsync(output);
    }

    [Theory]
    [InlineData(OcrMyPdfEngine.PngMediaType)]
    [InlineData(OcrMyPdfEngine.JpegMediaType)]
    public async Task CreateArchive_RecognizesTheText_OfAnImage(string mediaType)
    {
        OcrSamples.SkipUnlessToolsInstalled();
        var image = mediaType == OcrMyPdfEngine.PngMediaType ? samples.Png : samples.Jpeg;

        var (result, output) = await CreateArchiveAsync(image, mediaType);

        Assert.Equal(1, result.PageCount);
        Assert.Contains(OcrSamples.GermanWord, result.Text, StringComparison.Ordinal);
        await AssertIsPdfAAsync(output);
    }

    [Fact]
    public async Task CreateArchive_RecognizesTheText_OfAPngWithAlphaChannel()
    {
        OcrSamples.SkipUnlessToolsInstalled();

        var (result, output) = await CreateArchiveAsync(samples.RgbaPng, OcrMyPdfEngine.PngMediaType);

        Assert.Equal(1, result.PageCount);
        Assert.Contains(OcrSamples.GermanWord, result.Text, StringComparison.Ordinal);
        await AssertIsPdfAAsync(output);
    }

    [Fact]
    public async Task CreateArchive_ProcessesASignedPdf_AndLeavesTheOriginalUnchanged()
    {
        OcrSamples.SkipUnlessToolsInstalled();
        Assert.SkipWhen(samples.SignedPdf is null, "pikepdf is not importable from python3 here; it comes with OCRmyPDF in CI and the Worker image.");
        var before = await File.ReadAllBytesAsync(samples.SignedPdf, Ct);

        var (result, output) = await CreateArchiveAsync(samples.SignedPdf, OcrMyPdfEngine.PdfMediaType);

        Assert.Contains("Rechnung Stromanbieter Januar", result.Text, StringComparison.Ordinal);
        await AssertIsPdfAAsync(output);
        Assert.Equal(before, await File.ReadAllBytesAsync(samples.SignedPdf, Ct));
    }

    [Fact]
    public async Task CreateArchive_FailsPermanently_ForAnEncryptedPdf_AndCleansUp()
    {
        OcrSamples.SkipUnlessToolsInstalled();
        var work = samples.NewWorkFolder();
        var input = Path.Combine(work, "original");
        File.Copy(samples.EncryptedPdf, input);

        var error = await Assert.ThrowsAsync<ExternalToolException>(
            () => Engine().CreateArchiveAsync(input, OcrMyPdfEngine.PdfMediaType, Path.Combine(work, "archive.pdf"), correctPages: true, Ct));

        Assert.Equal(("ocrmypdf", 8, true), (error.Tool, error.ExitCode, error.IsPermanent));
        Assert.Equal([input], Directory.GetFileSystemEntries(work));
    }

    [Theory]
    [InlineData("ocrmypdf", 2, true)]
    [InlineData("ocrmypdf", 8, true)]
    [InlineData("ocrmypdf", 1, false)]
    [InlineData("ocrmypdf", 7, false)]
    [InlineData("ocrmypdf", 15, false)]
    [InlineData("img2pdf", 2, false)]
    [InlineData("pdftotext", 8, false)]
    public void IsPermanentFailure_IsTrueOnlyForOcrMyPdfsInputErrors(string tool, int exitCode, bool permanent) =>
        Assert.Equal(permanent, OcrMyPdfEngine.IsPermanentFailure(tool, exitCode));

    [Fact]
    public async Task CreateArchive_RecognizesTheText_OfAHeicPhoto()
    {
        OcrSamples.SkipUnlessToolsInstalled();
        var heic = Path.Combine(samples.NewWorkFolder(), "photo.heic");
        var encoded = await TryRunAsync("heif-enc", ["-o", heic, samples.Jpeg]);
        Assert.SkipUnless(encoded, "No HEIC encoder here (heif-enc with libheif-plugin-x265); the Worker image only decodes.");

        var (result, output) = await CreateArchiveAsync(heic, OcrMyPdfEngine.HeicMediaType);

        Assert.Contains(OcrSamples.GermanWord, result.Text, StringComparison.Ordinal);
        await AssertIsPdfAAsync(output);
    }

    [Fact]
    public async Task CreateArchive_KeepsTheText_OfAPdfThatHasText()
    {
        OcrSamples.SkipUnlessToolsInstalled();

        var (result, output) = await CreateArchiveAsync(samples.TextPdf, OcrMyPdfEngine.PdfMediaType);

        Assert.Equal(2, result.PageCount);
        Assert.Contains("Rechnung Stromanbieter Januar", result.Text, StringComparison.Ordinal);
        await AssertIsPdfAAsync(output);
    }

    [Fact]
    public async Task CreateArchive_FailsPermanently_ForABrokenPdf_AndCleansUp()
    {
        OcrSamples.SkipUnlessToolsInstalled();
        var work = samples.NewWorkFolder();
        var input = Path.Combine(work, "original");
        await File.WriteAllTextAsync(input, "%PDF-1.7 this is not really a PDF", Ct);

        var error = await Assert.ThrowsAsync<ExternalToolException>(
            () => Engine().CreateArchiveAsync(input, OcrMyPdfEngine.PdfMediaType, Path.Combine(work, "archive.pdf"), correctPages: true, Ct));

        Assert.Equal(("ocrmypdf", 2, true), (error.Tool, error.ExitCode, error.IsPermanent));
        Assert.Equal([input], Directory.GetFileSystemEntries(work));
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("image/gif")]
    [InlineData("application/pdf; evil")]
    public async Task CreateArchive_RejectsMediaTypesItCannotProcess(string mediaType)
    {
        var work = Directory.CreateTempSubdirectory("nomorepaper-ocr-");
        try
        {
            await Assert.ThrowsAsync<NotSupportedException>(
                () => Engine().CreateArchiveAsync(Path.Combine(work.FullName, "original"), mediaType, Path.Combine(work.FullName, "archive.pdf"), correctPages: true, Ct));

            Assert.Empty(work.GetFileSystemInfos());
        }
        finally
        {
            work.Delete(recursive: true);
        }
    }

    private async Task<bool> TryRunAsync(string tool, string[] args)
    {
        try
        {
            var (exitCode, _) = await _process.RunAsync(tool, args, TimeSpan.FromMinutes(2), Ct);
            return exitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private OcrMyPdfEngine Engine() => new(_process, TimeProvider.System, NullLogger<OcrMyPdfEngine>.Instance);

    /// <returns>The result and the path of the archive PDF, which is the only new file in the working folder.</returns>
    private async Task<(OcrResult Result, string Output)> CreateArchiveAsync(string sample, string mediaType)
    {
        var work = samples.NewWorkFolder();
        var input = Path.Combine(work, "original");
        File.Copy(sample, input);
        var output = Path.Combine(work, "archive.pdf");

        var result = await Engine().CreateArchiveAsync(input, mediaType, output, correctPages: true, Ct);

        Assert.Equal(new[] { input, output }.Order(StringComparer.Ordinal), Directory.GetFileSystemEntries(work).Order(StringComparer.Ordinal));
        return (result, output);
    }

    private static async Task AssertIsPdfAAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path, Ct);
        var text = Encoding.Latin1.GetString(bytes);

        // OCRmyPDF writes the PDF/A conformance (pdfaid:part) into the uncompressed XMP metadata.
        Assert.StartsWith("%PDF-", text, StringComparison.Ordinal);
        Assert.Contains("pdfaid:part", text, StringComparison.Ordinal);
    }
}
