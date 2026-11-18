using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

public sealed class DocumentFileTests
{
    internal const string Sha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    [Fact]
    public void Constructor_KeepsTheValues()
    {
        var file = new DocumentFile(DocumentFileKind.Archive, "archive.pdf", "application/pdf", 42, Sha256);

        Assert.Equal(DocumentFileKind.Archive, file.Kind);
        Assert.Equal("archive.pdf", file.FileName);
        Assert.Equal("application/pdf", file.MediaType);
        Assert.Equal(42, file.SizeBytes);
        Assert.Equal(Sha256, file.Sha256);
    }

    [Theory]
    [InlineData("")]
    [InlineData("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855")]
    [InlineData("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b85")]
    [InlineData("g3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    public void Constructor_RejectsAnythingButLowercaseSha256Hex(string sha256) =>
        Assert.Throws<ArgumentException>(() => new DocumentFile(DocumentFileKind.Original, "a.pdf", "application/pdf", 1, sha256));

    [Theory]
    [InlineData("", "application/pdf")]
    [InlineData("a.pdf", " ")]
    public void Constructor_RequiresFileNameAndMediaType(string fileName, string mediaType) =>
        Assert.ThrowsAny<ArgumentException>(() => new DocumentFile(DocumentFileKind.Original, fileName, mediaType, 1, Sha256));

    [Fact]
    public void Constructor_RejectsTooLongNames()
    {
        var tooLong = new string('a', DocumentFile.MaxFileNameLength + 1);

        Assert.Throws<ArgumentException>(() => new DocumentFile(DocumentFileKind.Original, tooLong, "application/pdf", 1, Sha256));
        Assert.Throws<ArgumentException>(() => new DocumentFile(DocumentFileKind.Original, "a.pdf", tooLong, 1, Sha256));
    }

    [Fact]
    public void Constructor_RejectsANegativeSize() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentFile(DocumentFileKind.Original, "a.pdf", "application/pdf", -1, Sha256));

    [Fact]
    public void Constructor_RejectsAnUnknownKind() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new DocumentFile((DocumentFileKind)99, "a.pdf", "application/pdf", 1, Sha256));

    internal static DocumentFile File(DocumentFileKind kind) =>
        new(kind, kind == DocumentFileKind.Original ? "scan.jpg" : "archive.pdf", "application/pdf", 10, Sha256);
}
