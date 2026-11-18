using NoMorePaper.Server.Documents;
using NoMorePaper.Server.Tests.Hosting;
using Xunit;

namespace NoMorePaper.Server.Tests.Documents;

public sealed class FileSignatureTests
{
    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/heic")]
    public void DetectMediaType_RecognizesAcceptedTypes(string expected)
    {
        var file = expected switch
        {
            "application/pdf" => SampleFiles.Pdf(),
            "image/jpeg" => SampleFiles.Jpeg(),
            "image/png" => SampleFiles.Png(),
            _ => SampleFiles.Heic(),
        };

        Assert.Equal(expected, FileSignature.DetectMediaType(file.AsSpan(0, FileSignature.HeadLength)));
    }

    [Theory]
    [InlineData("heix")]
    [InlineData("mif1")]
    [InlineData("heif")]
    public void DetectMediaType_AcceptsTheOtherHeifBrands(string brand)
    {
        byte[] head = [0, 0, 0, 0x18, .. "ftyp"u8, .. System.Text.Encoding.ASCII.GetBytes(brand), 0, 0, 0, 0];

        Assert.Equal("image/heic", FileSignature.DetectMediaType(head));
    }

    [Fact]
    public void DetectMediaType_RejectsOtherTypes()
    {
        Assert.Null(FileSignature.DetectMediaType(SampleFiles.Text()));
        Assert.Null(FileSignature.DetectMediaType("<html><script>"u8));
        Assert.Null(FileSignature.DetectMediaType("MZ\x90\0"u8));
    }

    [Fact]
    public void DetectMediaType_RejectsMp4AndUnknownIsoBrands()
    {
        byte[] mp4 = [0, 0, 0, 0x18, .. "ftypmp42"u8, 0, 0, 0, 0];

        Assert.Null(FileSignature.DetectMediaType(mp4));
    }

    [Fact]
    public void DetectMediaType_HandlesShortInput()
    {
        Assert.Null(FileSignature.DetectMediaType([]));
        Assert.Null(FileSignature.DetectMediaType([0xFF, 0xD8]));
        Assert.Null(FileSignature.DetectMediaType("%PDF"u8));
        Assert.Null(FileSignature.DetectMediaType([0, 0, 0, 0x18, .. "ftyp"u8]));
    }
}
