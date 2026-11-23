using System.Globalization;
using System.Text;

namespace NoMorePaper.UI.Tests.Editing;

/// <summary>PDFs for the page editor tests; the pages are told apart by what is drawn on them.</summary>
internal static class EditingPdfs
{
    /// <summary>A point inside the blue box of page 2 (fractions of the page from the top left).</summary>
    public static readonly (double X, double Y) BoxPoint = (0.29, 0.23);

    /// <summary>Page 1 and 3 have text only; page 2 has a blue box and no text.</summary>
    public static byte[] ThreePages()
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
            Page(7),
            Page(8),
            Page(9),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream("BT /F1 24 Tf 72 760 Td (Seite eins) Tj ET"),
            Stream("0 0 1 rg 72 600 200 100 re f"),
            Stream("BT /F1 24 Tf 72 760 Td (Seite drei) Tj ET"),
        ];
        // Object 6 is the font; pages are objects 3 to 5.
        return Build(objects);
    }

    /// <summary>
    /// Page 1 and 2 are a photographed sheet: a white sheet of 450 × 650 pt, turned by 3° about the page centre, on a
    /// dark background (as a scan with the lid open). Its edges are 2–3 cm from the page edges. Page 3 has text only:
    /// no sheet edge to find.
    /// </summary>
    public static byte[] SkewedSheets()
    {
        // cos 3°, sin 3° and the shift that keeps the sheet's centre on the page centre (297.5, 421).
        const string Sheet = "0.3 g 0 0 595 842 re f q 0.99863 0.05234 -0.05234 0.99863 89.818 84.669 cm "
            + "1 g 0 0 450 650 re f 0 g BT /F1 20 Tf 40 590 Td (Rechnung) Tj 0 -30 Td (Betrag 42 Euro) Tj ET Q";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
            Page(7),
            Page(8),
            Page(9),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream(Sheet),
            Stream(Sheet),
            Stream("BT /F1 24 Tf 72 760 Td (Seite drei) Tj ET"),
        ];
        return Build(objects);
    }

    private static string Page(int contents) => string.Create(
        CultureInfo.InvariantCulture,
        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 6 0 R >> >> /Contents {contents} 0 R >>");

    private static byte[] Build(string[] objects)
    {
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        pdf.Append(CultureInfo.InvariantCulture,
            $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        // Only ASCII above, so characters and bytes line up for the offsets.
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string Stream(string content) =>
        string.Create(CultureInfo.InvariantCulture, $"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
}
