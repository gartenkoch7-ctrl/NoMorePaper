using NoMorePaper.Application.Documents;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Documents;

public sealed class DocumentNamesTests
{
    [Theory]
    [InlineData("scan.pdf", "scan.pdf")]
    [InlineData("C:\\Users\\x\\scan.pdf", "scan.pdf")]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData("a\0b\r\n.pdf", "ab.pdf")]
    [InlineData("  spaced name.pdf  ", "spaced name.pdf")]
    [InlineData("", "document")]
    [InlineData(null, "document")]
    [InlineData("dir/", "document")]
    public void FileName_KeepsOnlyASafeLastSegment(string? received, string expected) =>
        Assert.Equal(expected, DocumentNames.FileName(received));

    [Fact]
    public void FileName_ShortensLongNames_ButKeepsTheExtension()
    {
        var name = DocumentNames.FileName(new string('a', 400) + ".pdf");

        Assert.Equal(DocumentFile.MaxFileNameLength, name.Length);
        Assert.EndsWith(".pdf", name, StringComparison.Ordinal);
    }

    [Fact]
    public void FileName_DropsAbsurdExtensions_WhenShortening()
    {
        var name = DocumentNames.FileName("a." + new string('b', 400));

        Assert.True(name.Length <= DocumentFile.MaxFileNameLength);
    }

    [Theory]
    [InlineData("Zahnarzt 2026.pdf", "Zahnarzt 2026")]
    [InlineData("archive.tar.gz", "archive.tar")]
    [InlineData(".pdf", "document")]
    [InlineData("", "document")]
    [InlineData("noextension", "noextension")]
    public void Title_IsTheNameWithoutExtension(string received, string expected) =>
        Assert.Equal(expected, DocumentNames.Title(received));

    [Fact]
    public void Title_IsShortenedToTheModelLimit()
    {
        // The file name is cut to 255 first, so the title is never longer than that.
        var title = DocumentNames.Title(new string('t', 900) + ".pdf");

        Assert.InRange(title.Length, 1, Document.MaxTitleLength);
    }
}
