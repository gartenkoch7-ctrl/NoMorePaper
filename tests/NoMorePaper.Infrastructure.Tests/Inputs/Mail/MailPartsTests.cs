using NoMorePaper.Infrastructure.Inputs.Mail;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.Mail;

/// <summary>Image sizes from headers, HTML to text, and the key of a mail.</summary>
public sealed class MailPartsTests
{
    [Fact]
    public void ImageHeader_ReadsJpegAndPng_WithoutDecoding()
    {
        Assert.Equal((2480, 3508), ImageHeader.Size(TestMessages.Jpeg(2480, 3508, 64)));
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");
        Assert.Equal((1, 1), ImageHeader.Size(png));
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF })]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x01 })]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xDA, 0x00, 0x08, 0, 0, 0, 0, 0, 0 })]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 1, 0, 1, 0 })]
    public void ImageHeader_GivesUpOnAnythingElse(byte[] data) => Assert.Null(ImageHeader.Size(data));

    [Fact]
    public void HtmlText_KeepsBlocksAndLinks_DropsScriptsAndStyles()
    {
        const string html = """
            <html><head><title>T</title><style>.x{color:red}</style><script>alert('x')</script></head>
            <body><p>Guten&nbsp;Tag,</p><p>Betrag:   <b>17,39 EUR</b></p>
            <table><tr><td>Tarif</td><td>14,99</td></tr></table>
            <a href="https://portal.example/r?id=1">Zum Portal</a><a href="mailto:x@y.example">Mail</a></body></html>
            """;

        var text = HtmlText.Convert(html);

        Assert.Equal("Guten Tag,\n\nBetrag: 17,39 EUR\n\nTarif  14,99\n\nZum Portal (https://portal.example/r?id=1)Mail", text);
        Assert.DoesNotContain("alert", text, StringComparison.Ordinal);
        Assert.DoesNotContain("color", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ItemKey_IsTheMessageIdWithDate_OrAHashOfTheEnvelope()
    {
        var date = new DateTimeOffset(2026, 11, 19, 8, 0, 0, TimeSpan.FromHours(1));

        Assert.Equal("id:a1@b.example|2026-11-19T07:00:00.0000000+00:00", MailMessageReader.ItemKey("<a1@b.example>", date, "x@b.example", "S", 10));
        var hashed = MailMessageReader.ItemKey(null, date, "x@b.example", "S", 10);
        Assert.StartsWith("hash:", hashed, StringComparison.Ordinal);
        Assert.Equal(hashed, MailMessageReader.ItemKey(" ", date, "x@b.example", "S", 10));
        Assert.NotEqual(hashed, MailMessageReader.ItemKey(null, date, "x@b.example", "S", 11));
    }
}
