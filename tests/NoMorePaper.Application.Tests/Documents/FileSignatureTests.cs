using NoMorePaper.Application.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Documents;

public sealed class FileSignatureTests
{
    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/jpeg")]
    [InlineData("image/png")]
    [InlineData("image/heic")]
    public void DetectMediaType_RecognizesAcceptedTypes(string expected)
    {
        byte[] head = expected switch
        {
            "application/pdf" => [.. "%PDF-1.7\n"u8],
            "image/jpeg" => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46],
            "image/png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00],
            _ => [0x00, 0x00, 0x00, 0x18, .. "ftypheic"u8, 0x00, 0x00, 0x00, 0x00],
        };
        byte[] file = [.. head, .. new byte[100]];

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
        Assert.Null(FileSignature.DetectMediaType("Just some text, not a document."u8));
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
