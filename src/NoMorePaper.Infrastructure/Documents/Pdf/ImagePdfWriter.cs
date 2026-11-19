using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace NoMorePaper.Infrastructure.Documents.Pdf;

/// <summary>
/// Joins photographed pages (JPEG) into one PDF, one page per image, the image filling its page. The
/// JPEG data is embedded as it is, not recompressed.
/// </summary>
internal sealed class ImagePdfWriter(double shortSide = ImagePdfWriter.ShortSidePoints)
{
    /// <summary>Width of an A4 page in points: the shorter side of every page, so OCR sees a sensible resolution.</summary>
    public const double ShortSidePoints = 595.28;

    public int Write(IReadOnlyList<Stream> jpegs, Stream target)
    {
        using var document = new PdfDocument();
        foreach (var jpeg in jpegs)
        {
            using var image = XImage.FromStream(jpeg);
            var scale = shortSide / Math.Min(image.PixelWidth, image.PixelHeight);
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(image.PixelWidth * scale);
            page.Height = XUnit.FromPoint(image.PixelHeight * scale);
            using var graphics = XGraphics.FromPdfPage(page);
            graphics.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
        }

        var pages = document.PageCount;
        document.Save(target, closeStream: false);
        return pages;
    }
}
