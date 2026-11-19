using System.Text;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Infrastructure.Inputs.Mail;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Inputs.Mail;

/// <summary>Only the own server's verdict on the sender domain counts; anything else is unknown.</summary>
public sealed class AuthenticationResultsTests
{
    private const string Trusted = "mx.haushalt.example";
    private const string Domain = "bank.example";

    [Theory]
    [InlineData("mx.haushalt.example; dmarc=pass header.from=bank.example", SenderCheck.Passed)]
    [InlineData("MX.Haushalt.Example 1; spf=pass smtp.mailfrom=info@bank.example", SenderCheck.Passed)]
    [InlineData("mx.haushalt.example; spf=pass smtp.mailfrom=bank.example; dkim=pass header.d=bank.example", SenderCheck.Passed)]
    [InlineData("mx.haushalt.example; dmarc=pass", SenderCheck.Passed)]
    [InlineData("mx.haushalt.example; dmarc=fail header.from=bank.example", SenderCheck.Failed)]
    [InlineData("mx.haushalt.example; spf=pass smtp.mailfrom=bank.example; dmarc=fail header.from=bank.example", SenderCheck.Failed)]
    [InlineData("mx.haushalt.example; spf=softfail smtp.mailfrom=bank.example", SenderCheck.Failed)]
    [InlineData("mx.haushalt.example; spf=pass smtp.mailfrom=bounces.newsletter.example", SenderCheck.Unknown)]
    [InlineData("mx.haushalt.example; dmarc=pass header.from=other.example", SenderCheck.Unknown)]
    [InlineData("mx.haushalt.example; spf=pass", SenderCheck.Unknown)]
    [InlineData("mx.haushalt.example; dkim=pass header.d=bank.example", SenderCheck.Unknown)]
    [InlineData("mx.haushalt.example; none", SenderCheck.Unknown)]
    [InlineData("mx.other.example; dmarc=pass header.from=bank.example", SenderCheck.Unknown)]
    [InlineData("not a header at all ;;;", SenderCheck.Unknown)]
    public void OneHeader(string header, SenderCheck expected) =>
        Assert.Equal(expected, AuthenticationResults.Evaluate([Bytes(header)], Domain, Trusted));

    [Fact]
    public void OnlyTheTopmostHeaderOfTheTrustedServerCounts_AForgedOneBelowDoesNot()
    {
        // The own server adds its verdict on top; a header the sender put into the mail comes below.
        var headers = new[]
        {
            Bytes("mx.haushalt.example; dmarc=fail header.from=bank.example"),
            Bytes("mx.haushalt.example; dmarc=pass header.from=bank.example"),
        };

        Assert.Equal(SenderCheck.Failed, AuthenticationResults.Evaluate(headers, Domain, Trusted));
    }

    [Fact]
    public void AForeignHeaderOnTop_IsSkipped()
    {
        var headers = new[] { Bytes("relay.example; dmarc=pass header.from=bank.example"), Bytes("mx.haushalt.example; dmarc=pass header.from=bank.example") };

        Assert.Equal(SenderCheck.Passed, AuthenticationResults.Evaluate(headers, Domain, Trusted));
    }

    [Theory]
    [InlineData(null, "bank.example")]
    [InlineData(" ", "bank.example")]
    [InlineData(Trusted, "")]
    public void WithoutTrustedIdOrSenderDomain_NothingCounts(string? trusted, string domain) =>
        Assert.Equal(SenderCheck.Unknown, AuthenticationResults.Evaluate([Bytes("mx.haushalt.example; dmarc=pass header.from=bank.example")], domain, trusted));

    private static byte[] Bytes(string header) => Encoding.ASCII.GetBytes(" " + header + "\r\n");
}
