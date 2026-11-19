using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NoMorePaper.Application.Documents;
using NoMorePaper.Infrastructure.Documents.Pdf;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs;

/// <summary>Camera pages and mail texts as PDF, with the embedded font (no system fonts needed).</summary>
public sealed class PdfSharpComposerTests
{
    private readonly PdfSharpComposer _composer = new(TimeProvider.System, NullLogger<PdfSharpComposer>.Instance);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Images_BecomeOnePagePerImage_InTheirShape()
    {
        await using var portrait = File.OpenRead(Sample("portrait.jpg"));
        await using var landscape = File.OpenRead(Sample("landscape.jpg"));
        using var pdf = new MemoryStream();

        await _composer.ComposeImagesAsync([portrait, landscape], pdf, Ct);

        using var document = Open(pdf);
        Assert.Equal(2, document.PageCount);
        Assert.Equal(595.28, document.Pages[0].Width.Point, 1);
        Assert.Equal(892.92, document.Pages[0].Height.Point, 1);
        Assert.True(document.Pages[1].Width.Point > document.Pages[1].Height.Point);
    }

    [Fact]
    public async Task NoImages_AreRefused() =>
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _composer.ComposeImagesAsync([], new MemoryStream(), Ct));

    [Fact]
    public async Task AnUnreadableImage_FailsWithoutWritingAnything()
    {
        using var target = new MemoryStream();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            _composer.ComposeImagesAsync([new MemoryStream("no image"u8.ToArray())], target, Ct));

        Assert.Equal(0, target.Length);
    }

    [Fact]
    public async Task AShortText_FitsOnOneA4Page_WithTheFontEmbedded()
    {
        var page = new TextPage(
            "Mail ohne Anhang",
            [new TextPageField("Von", "rechnung@stadtwerke.example"), new TextPageField("Betreff", "Abschlag für Dezember – 84,20 €")],
            "Sehr geehrte Kundin,\r\n\r\nder Abschlag wird am 30.11. abgebucht.\tGrüße");
        using var pdf = new MemoryStream();

        await _composer.ComposeTextAsync(page, pdf, Ct);

        using var document = Open(pdf);
        Assert.Equal(1, document.PageCount);
        Assert.Equal((595.28, 841.89), (Math.Round(document.Pages[0].Width.Point, 2), Math.Round(document.Pages[0].Height.Point, 2)));
        var raw = Encoding.Latin1.GetString(pdf.ToArray());
        Assert.Contains("+DejaVu", raw, StringComparison.Ordinal);
        Assert.Contains("/FontFile2", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALongText_WrapsAndBreaksIntoPages()
    {
        var words = string.Join(' ', Enumerable.Repeat("Vertragsbedingungen", 1500));
        var longWord = new string('x', 5000);
        using var pdf = new MemoryStream();

        await _composer.ComposeTextAsync(new TextPage("Lang", [], words + "\n" + longWord), pdf, Ct);

        using var document = Open(pdf);
        Assert.True(document.PageCount > 3, $"Only {document.PageCount} pages.");
    }

    private static PdfDocument Open(MemoryStream pdf)
    {
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf.ToArray(), 0, 5), StringComparison.Ordinal);
        pdf.Position = 0;
        return PdfReader.Open(pdf, PdfDocumentOpenMode.Import);
    }

    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "Inputs", "Samples", name);
}
