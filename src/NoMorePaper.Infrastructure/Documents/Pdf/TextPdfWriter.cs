using System.Text;
using NoMorePaper.Application.Documents;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace NoMorePaper.Infrastructure.Documents.Pdf;

/// <summary>
/// Sets a <see cref="TextPage"/> on A4 pages with 20 mm margins: the heading in bold, the header fields,
/// then the body. Lines are wrapped at word boundaries (words longer than a line are split) and a new
/// page starts when one is full. The text stays text, so OCR and search read it as it is.
/// </summary>
internal sealed class TextPdfWriter(double fontSize = TextPdfWriter.BodySize)
{
    public const double BodySize = 11;

    private const double HeadingSize = 16;
    private const double PageWidth = 595.28;
    private const double PageHeight = 841.89;
    private const double Margin = 56.69;
    private const double LineSpacing = 1.35;

    public int Write(TextPage content, Stream target)
    {
        using var document = new PdfDocument();
        var body = new XFont(EmbeddedFontResolver.Family, fontSize, XFontStyleEx.Regular);
        var bold = new XFont(EmbeddedFontResolver.Family, fontSize, XFontStyleEx.Bold);
        var heading = new XFont(EmbeddedFontResolver.Family, HeadingSize, XFontStyleEx.Bold);
        var pen = new PageCursor(document);
        try
        {
            pen.Paragraph(Clean(content.Heading), heading);
            pen.Space(fontSize);
            foreach (var field in content.Header)
            {
                pen.Paragraph($"{Clean(field.Label)}: {Clean(field.Value)}", bold);
            }

            pen.Space(fontSize);
            foreach (var paragraph in Clean(content.Body, keepLineBreaks: true).Split('\n'))
            {
                pen.Paragraph(paragraph, body);
            }
        }
        finally
        {
            pen.Dispose();
        }

        var pages = document.PageCount;
        document.Save(target, closeStream: false);
        return pages;
    }

    // Control characters cannot be drawn; tabs become spaces, other line breaks the one kind.
    private static string Clean(string text, bool keepLineBreaks = false)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n'))
        {
            builder.Append(c == '\n' && keepLineBreaks ? c : char.IsControl(c) ? ' ' : c);
        }

        return builder.ToString();
    }

    /// <summary>Where the next line goes; adds pages as they fill up.</summary>
    private sealed class PageCursor(PdfDocument document) : IDisposable
    {
        private const double Width = PageWidth - (2 * Margin);

        private XGraphics? _graphics;
        private double _y;

        public void Space(double height) => _y += height;

        public void Paragraph(string text, XFont font)
        {
            var lineHeight = font.Size * LineSpacing;
            foreach (var line in Lines(text, font))
            {
                if (_graphics is null || _y + lineHeight > PageHeight - Margin)
                {
                    NewPage();
                }

                _graphics!.DrawString(line, font, XBrushes.Black, new XRect(Margin, _y, Width, lineHeight), XStringFormats.TopLeft);
                _y += lineHeight;
            }
        }

        public void Dispose() => _graphics?.Dispose();

        private void NewPage()
        {
            _graphics?.Dispose();
            var page = document.AddPage();
            page.Width = XUnit.FromPoint(PageWidth);
            page.Height = XUnit.FromPoint(PageHeight);
            _graphics = XGraphics.FromPdfPage(page);
            _y = Margin;
        }

        // An empty paragraph is an empty line, so blank lines of the body stay.
        private List<string> Lines(string text, XFont font)
        {
            var lines = new List<string>();
            var line = string.Empty;
            foreach (var word in text.Split(' '))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (Fits(candidate, font))
                {
                    line = candidate;
                    continue;
                }

                if (line.Length > 0)
                {
                    lines.Add(line);
                }

                line = word;
                while (line.Length > 1 && !Fits(line, font))
                {
                    var cut = LongestFit(line, font);
                    lines.Add(line[..cut]);
                    line = line[cut..];
                }
            }

            lines.Add(line);
            return lines;
        }

        // Binary search: a word longer than a line (a link, a code) may be thousands of characters.
        private int LongestFit(string word, XFont font)
        {
            var (low, high) = (1, word.Length - 1);
            while (low < high)
            {
                var middle = (low + high + 1) / 2;
                if (Fits(word[..middle], font))
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return low;
        }

        private bool Fits(string text, XFont font)
        {
            NewPageIfNone();
            return _graphics!.MeasureString(text, font).Width <= Width;
        }

        private void NewPageIfNone()
        {
            if (_graphics is null)
            {
                NewPage();
            }
        }
    }
}
