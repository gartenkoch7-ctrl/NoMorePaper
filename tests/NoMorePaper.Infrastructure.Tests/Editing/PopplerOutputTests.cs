using System.Text;
using NoMorePaper.Application.Editing;
using NoMorePaper.Domain.Editing;
using NoMorePaper.Infrastructure.Editing;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Editing;

/// <summary>Reading the output of the page tools, and the qpdf command line, without the tools.</summary>
public sealed class PopplerOutputTests
{
    [Fact]
    public void SplitPages_UsesTheFormFeedAfterEveryPage()
    {
        Assert.Equal(["Seite 1\n", "", "Seite 3\n"], PopplerOutput.SplitPages("Seite 1\n\f\fSeite 3\n\f"));
        Assert.Empty(PopplerOutput.SplitPages(string.Empty));
    }

    [Fact]
    public void Head_CollapsesWhitespace_AndCutsAtTheLimit()
    {
        Assert.Equal("Sehr geehrte Frau Muster, anbei", PopplerOutput.Head("  Sehr  geehrte\n\nFrau Muster,\tanbei  ", PageInfo.MaxHeadLength));
        Assert.Equal("abc de", PopplerOutput.Head("abc  def ghi", 6));
        Assert.Equal(300, PopplerOutput.Head(new string('x', 400), PageInfo.MaxHeadLength).Length);
        Assert.Equal("a", PopplerOutput.Head("a😀", 2));
    }

    [Fact]
    public void VisibleCharacters_IgnoresWhitespace() =>
        Assert.Equal(4, PopplerOutput.VisibleCharacters(" a b\n\tc\0 d "));

    [Fact]
    public void HasFonts_ReadsTheListingOfPdffonts()
    {
        const string Header = "name                                 type              encoding         emb sub uni object ID\n"
            + "------------------------------------ ----------------- ---------------- --- --- --- ---------\n";

        Assert.False(PopplerOutput.HasFonts(Header));
        Assert.True(PopplerOutput.HasFonts(Header + "Helvetica                            Type 1            Custom           no  no  no       5  0\n"));
    }

    [Fact]
    public void DarkShare_CountsPixelsDarkerThanHalfGrey()
    {
        var image = Pgm(4, 2, [0, 255, 255, 255, 127, 128, 255, 255], comment: true);

        Assert.Equal(0.25, PopplerOutput.DarkShare(image));
        Assert.Equal(0, PopplerOutput.DarkShare(Pgm(0, 0, [])));
        Assert.Throws<FormatException>(() => PopplerOutput.DarkShare("P6\n1 1\n255\n\0\0\0"u8));
        Assert.Throws<FormatException>(() => PopplerOutput.DarkShare(Pgm(2, 2, [0])));
    }

    [Fact]
    public void QpdfArguments_TurnFirst_ThenGroupPagesOfOneFile()
    {
        var a = Path.GetFullPath("a.pdf");
        var b = Path.GetFullPath("b.pdf");
        var output = Path.GetFullPath("out.pdf");

        var args = QpdfTool.Arguments(
            [new(a, 3, PageRotation.None), new(a, 1, PageRotation.Right), new(b, 2, PageRotation.Left), new(a, 2, PageRotation.Half)],
            output);

        Assert.Equal(["--rotate=+90:2", "--rotate=+270:3", "--rotate=+180:4", "--deterministic-id", "--empty", "--pages", a, "3,1", b, "2", a, "2", "--", output], args);
        Assert.Throws<ArgumentException>(() => QpdfTool.Arguments([new(a, 0, PageRotation.None)], output));
    }

    private static byte[] Pgm(int width, int height, byte[] pixels, bool comment = false)
    {
        var header = Encoding.ASCII.GetBytes($"P5\n{(comment ? "# pdftoppm\n" : string.Empty)}{width} {height}\n255\n");
        return [.. header, .. pixels];
    }
}
