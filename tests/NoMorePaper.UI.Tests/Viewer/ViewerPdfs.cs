using System.Globalization;
using System.Text;

namespace NoMorePaper.UI.Tests.Viewer;

/// <summary>PDFs for the viewer tests: the shader spike's test documents and generated ones.</summary>
internal static class ViewerPdfs
{
    /// <summary>A sample from <c>spikes/viewer-shader/samples</c> (all invented, see its README).</summary>
    public static byte[] Sample(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "NoMorePaper.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Repository root (NoMorePaper.slnx) not found.");
        }

        return File.ReadAllBytes(Path.Combine(directory.FullName, "spikes", "viewer-shader", "samples", fileName));
    }

    /// <summary>Page 1 has the text "Seite eins mit Text", page 2 only a blue box and no text.</summary>
    public static byte[] TwoPages()
    {
        const string text = "BT /F1 24 Tf 72 760 Td (Seite eins mit Text) Tj ET";
        const string box = "0 0 1 rg 72 600 200 100 re f";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 6 0 R >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 7 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream(text),
            Stream(box),
        ];
        return Build(objects);
    }

    /// <summary>A point inside the box of <see cref="BlueBox"/> (fractions of the page from the top left).</summary>
    public static readonly (double X, double Y) BoxPoint = (0.5, 0.45);

    /// <summary>A point on bare paper of <see cref="BlueBox"/> (fractions of the page from the top left).</summary>
    public static readonly (double X, double Y) PaperPoint = (0.05, 0.05);

    /// <summary>
    /// One page of <paramref name="width"/> x <paramref name="height"/> points: a pure blue box over
    /// the middle (x 20–80 %, y 30–60 % from the top) and the text "Blaue Box".
    /// </summary>
    public static byte[] BlueBox(int width, int height)
    {
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            string.Create(CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>"),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream(BlueBoxContent(width, height)),
        ];
        return Build(objects);
    }

    /// <summary>
    /// Two A4 pages: page 1 is grey paper with black text, so its analysis finds a grey paper
    /// white; page 2 is <see cref="BlueBox"/>. Drawn with page 1's analysis, page 2's box is off.
    /// </summary>
    public static byte[] GreyPaperThenBlueBox()
    {
        const string grey = "0.7 g 0 0 595 842 re f 0 g BT /F1 24 Tf 72 760 Td (Graues Papier) Tj ET";
        static string Page(int contents) => string.Create(CultureInfo.InvariantCulture,
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents {contents} 0 R >>");
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
            Page(6),
            Page(7),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            Stream(grey),
            Stream(BlueBoxContent(595, 842)),
        ];
        return Build(objects);
    }

    private static string BlueBoxContent(int width, int height) => string.Create(CultureInfo.InvariantCulture,
        $"0 0 1 rg {width / 5} {height * 2 / 5} {width * 3 / 5} {height * 3 / 10} re f BT /F1 {height / 30} Tf {width / 10} {height / 5} Td (Blaue Box) Tj ET");

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
