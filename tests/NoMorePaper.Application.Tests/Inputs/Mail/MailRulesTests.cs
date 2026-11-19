using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Application.Recognition;
using Xunit;

namespace NoMorePaper.Application.Tests.Inputs.Mail;

/// <summary>The pure rules of the mail input: routing, the text for the LLM, the stored set-up.</summary>
public sealed class MailRulesTests
{
    private static readonly Guid s_anna = Guid.CreateVersion7();

    [Theory]
    [InlineData(SenderCheck.Passed, true, false, true, false)]
    [InlineData(SenderCheck.Unknown, true, false, false, true)]
    [InlineData(SenderCheck.Failed, true, false, false, true)]
    [InlineData(SenderCheck.Passed, false, false, false, true)]
    [InlineData(SenderCheck.Unknown, false, true, false, false)]
    [InlineData(SenderCheck.Passed, true, true, true, false)]
    public void Routing_TrustsAnAccountsAddressOnlyWhenConfirmed(SenderCheck check, bool hasAccount, bool backFromSpam, bool ownedByAnna, bool spamCheck)
    {
        var routing = MailRouting.Decide(hasAccount ? s_anna : null, check, backFromSpam);

        Assert.Equal((ownedByAnna ? s_anna : (Guid?)null, spamCheck), (routing.OwnerId, routing.NeedsSpamCheck));
    }

    [Fact]
    public void Text_ShowsLinksAsTheirHost_AndKeepsHeadAndTailOfALongMail()
    {
        var body = "Bitte bestätigen Sie hier: https://login.bank-sicher.example/x?id=123456789&t=abc\n"
            + string.Concat(Enumerable.Repeat("Zeile mit Text für die Länge.\n", 200)) + "Abmelden: https://news.example/u/1";

        var text = MailText.From(MailHarness.Mail() with { PlainText = body, Subject = new string('B', 400) }, ["application/pdf"]);

        Assert.StartsWith("Bitte bestätigen Sie hier: [Link: login.bank-sicher.example]\n", text.Body, StringComparison.Ordinal);
        Assert.EndsWith("Abmelden: [Link: news.example]", text.Body, StringComparison.Ordinal);
        Assert.Contains(RecognitionText.Marker, text.Body, StringComparison.Ordinal);
        Assert.InRange(text.Body.Length, MailText.MaxLength - 100, MailText.MaxLength + RecognitionText.Marker.Length);
        Assert.Equal((300, "stadtwerke.example"), (text.Subject.Length, text.SenderDomain));
    }

    [Theory]
    [InlineData("Info@Bank.Example", "bank.example")]
    [InlineData("<a@b.example>", "b.example")]
    [InlineData("", "")]
    [InlineData("kein-at", "")]
    public void Domain_IsThePartAfterTheAt(string address, string expected) => Assert.Equal(expected, MailText.Domain(address));

    [Fact]
    public void Settings_RoundTripAsJson_WithoutPassword()
    {
        var settings = new MailboxSettings("imap.example", 993, MailboxSecurity.Ssl, "dokumente@example", "mx.example");

        var json = settings.ToJson();

        Assert.Equal(settings, MailboxSettings.Parse(json));
        Assert.Contains("\"security\":\"Ssl\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("", 993, "user")]
    [InlineData("imap example", 993, "user")]
    [InlineData("imap.example", 0, "user")]
    [InlineData("imap.example", 70000, "user")]
    [InlineData("imap.example", 993, "")]
    public void Settings_RefuseWhatCannotConnect(string host, int port, string user)
    {
        Assert.False(new MailboxSettings(host, port, MailboxSecurity.StartTls, user, null).IsValid);
        Assert.Null(MailboxSettings.Parse("{\"host\":\"" + host + "\",\"port\":" + port + ",\"security\":\"None\",\"userName\":\"" + user + "\"}"));
    }

    [Fact]
    public void Settings_RefuseNoJson() => Assert.Null(MailboxSettings.Parse("not json"));
}
