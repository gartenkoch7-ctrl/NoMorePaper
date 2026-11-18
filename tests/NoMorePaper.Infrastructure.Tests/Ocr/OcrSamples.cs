using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Infrastructure.Ocr;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Ocr;

/// <summary>
/// Sample documents with known text, generated once per test class with Ghostscript (installed
/// with OCRmyPDF), img2pdf and pikepdf: a PDF with a text layer, the same encrypted and marked as
/// signed, scans as PNG (also with alpha channel) and JPEG and an image-only two-page PDF. Nothing
/// is generated where the OCR tools are missing; those tests are skipped.
/// </summary>
public sealed class OcrSamples : IAsyncLifetime
{
    public const string GermanWord = "Stromanbieter";
    public const string EnglishWord = "electricity";

    // Two pages: "Seite" (page) and its number identify each page in the recognized text.
    private const string PostScript =
        """
        %!PS
        /Helvetica findfont 30 scalefont setfont
        72 700 moveto (Rechnung Stromanbieter Januar) show
        72 650 moveto (Invoice for electricity) show
        72 600 moveto (Seite 1) show
        showpage
        /Helvetica findfont 30 scalefont setfont
        72 700 moveto (Zahlungsfrist Februar) show
        72 650 moveto (Seite 2) show
        showpage
        """;

    // SigFlags 3: the file contains signatures and must only be appended to (PDF 32000-1, 12.7.2).
    private const string MarkAsSigned =
        """
        import sys, pikepdf
        with pikepdf.open(sys.argv[1]) as pdf:
            pdf.Root.AcroForm = pdf.make_indirect(pikepdf.Dictionary(Fields=pikepdf.Array(), SigFlags=3))
            pdf.save(sys.argv[2])
        """;

    private static readonly TimeSpan s_timeout = TimeSpan.FromMinutes(2);
    private readonly ExternalProcess _process = new(TimeProvider.System, NullLogger<ExternalProcess>.Instance);
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("nomorepaper-ocr-samples-");

    /// <summary>Whether ocrmypdf is on <c>PATH</c>; it brings Ghostscript and comes with img2pdf in CI and the Worker image.</summary>
    public static bool ToolsInstalled { get; } = IsOnPath("ocrmypdf");

    /// <summary>PDF whose pages already contain text.</summary>
    public string TextPdf => Sample("text.pdf");

    /// <summary>Two-page PDF of scanned images without text.</summary>
    public string ScannedPdf => Sample("scanned.pdf");

    /// <summary>First page as a 300 dpi PNG.</summary>
    public string Png => Sample("page-1.png");

    /// <summary>First page as a 300 dpi JPEG.</summary>
    public string Jpeg => Sample("page-1.jpg");

    /// <summary>First page as a 300 dpi PNG with alpha channel (RGBA): black text on a transparent background.</summary>
    public string RgbaPng => Sample("alpha-1.png");

    /// <summary>The text PDF, encrypted with a user password.</summary>
    public string EncryptedPdf => Sample("encrypted.pdf");

    /// <summary>
    /// The text PDF marked as signed (<c>/SigFlags</c> in the form dictionary, which is what OCRmyPDF
    /// checks); <c>null</c> where pikepdf, which comes with OCRmyPDF, is not importable from <c>python3</c>.
    /// </summary>
    public string? SignedPdf { get; private set; }

    public static void SkipUnlessToolsInstalled() =>
        Assert.SkipUnless(ToolsInstalled, "ocrmypdf is not installed here; the OCR tests run in CI and in the Worker image.");

    public async ValueTask InitializeAsync()
    {
        if (!ToolsInstalled)
        {
            return;
        }

        var source = Sample("sample.ps");
        await File.WriteAllTextAsync(source, PostScript);
        await RunAsync("gs", [.. Ghostscript("pdfwrite"), "-o", TextPdf, source]);
        await RunAsync("gs", [.. Ghostscript("png16m"), "-r300", "-o", Sample("page-%d.png"), source]);
        await RunAsync("gs", [.. Ghostscript("jpeg"), "-r300", "-o", Sample("page-%d.jpg"), source]);
        await RunAsync("img2pdf", [Png, Sample("page-2.png"), "-o", ScannedPdf]);
        await RunAsync("gs", [.. Ghostscript("pngalpha"), "-r300", "-o", Sample("alpha-%d.png"), source]);
        await RunAsync("gs", [.. Ghostscript("pdfwrite"), "-sOwnerPassword=owner", "-sUserPassword=user", "-o", EncryptedPdf, source]);

        SignedPdf = await TryMarkAsSignedAsync(Sample("signed.pdf"));
    }

    private async Task<string?> TryMarkAsSignedAsync(string signed)
    {
        try
        {
            var (exitCode, _) = await _process.RunAsync("python3", ["-I", "-c", MarkAsSigned, TextPdf, signed], s_timeout, CancellationToken.None);
            return exitCode == 0 ? signed : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }

    public ValueTask DisposeAsync()
    {
        _folder.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }

    /// <summary>A new, empty folder for one test's output.</summary>
    public string NewWorkFolder() => _folder.CreateSubdirectory(Guid.NewGuid().ToString("N")).FullName;

    private static string[] Ghostscript(string device) => ["-q", "-dNOPAUSE", "-dBATCH", "-dSAFER", $"-sDEVICE={device}"];

    private string Sample(string fileName) => Path.Combine(_folder.FullName, fileName);

    private async Task RunAsync(string tool, string[] args)
    {
        var (exitCode, stdErrTail) = await _process.RunAsync(tool, args, s_timeout, CancellationToken.None);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"{tool} failed with exit code {exitCode}: {stdErrTail}");
        }
    }

    private static bool IsOnPath(string tool) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => File.Exists(Path.Combine(folder, tool)) || File.Exists(Path.Combine(folder, tool + ".exe")));
}
