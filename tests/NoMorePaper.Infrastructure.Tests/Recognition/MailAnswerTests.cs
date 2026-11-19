using NoMorePaper.Infrastructure.Recognition;
using Xunit;

namespace NoMorePaper.Infrastructure.Tests.Recognition;

/// <summary>The checks after the model's answer to the mail check.</summary>
public sealed class MailAnswerTests
{
    private const string Text = "Der Betrag von 38,40 EUR ist bis zum 03.12.2026 zu überweisen. Termin am 18. Dezember.";

    [Theory]
    [InlineData("Bitte bis 03.12.2026 überweisen.", true)]
    [InlineData("Bitte bis 3.12. überweisen.", true)]
    [InlineData("Frist: 2026-12-03.", true)]
    [InlineData("Termin am 18.12.2026.", true)]
    [InlineData("Bitte bis 04.12.2026 überweisen.", false)]
    [InlineData("Termin am 19. Dezember.", false)]
    [InlineData("Betrag 1.150,00 EUR ohne Datum.", true)]
    [InlineData("", true)]
    public void TheNotesDates_MustBeDatesOfTheText(string note, bool expected) =>
        Assert.Equal(expected, MailNoteCheck.DatesAreInText(note, Text));

    [Theory]
    [InlineData("Auf DE00 1234 5678 9000 0000 00 überweisen.", true)]
    [InlineData("Auf DE00 1234 5678 9000 0000 0000 überweisen.", false)]
    [InlineData("Vertragskonto 3300 5512 77.", true)]
    [InlineData("Kundennummer 7700-4412-09, Betrag 38,40 EUR.", true)]
    [InlineData("Kundennummer 123456.", false)]
    public void TheNotesLongNumbers_MustBeNumbersOfTheText(string note, bool expected) =>
        Assert.Equal(expected, MailNoteCheck.NumbersAreInText(note, "Konto DE00 1234 5678 9000 0000 00, Vertragskonto: 3300 5512 77, Kunde 7700-4412-09"));

    [Fact]
    public void Parse_ReadsTheVerdictsAndTheNote()
    {
        var answer = MailAnswer.Parse("""{"isSpam":false,"isDocument":true,"note":" Zahlung von **38,40 EUR** bis *03.12.2026*. "}""", false, Text, out var dropped);

        Assert.Equal((false, true, "Zahlung von 38,40 EUR bis 03.12.2026."), (answer.IsSpam, answer.IsDocument, answer.Note));
        Assert.False(dropped);
    }

    [Fact]
    public void Parse_DropsANoteWithAForeignDate_AndTheNoteOfSpam()
    {
        Assert.Null(MailAnswer.Parse("""{"isSpam":false,"isDocument":true,"note":"Zahlung bis 05.01.2027."}""", false, Text, out var dropped).Note);
        Assert.True(dropped);

        var spam = MailAnswer.Parse("""{"isSpam":true,"isDocument":false,"note":"Gewinn abholen."}""", false, Text, out _);
        Assert.Equal((true, (string?)null), (spam.IsSpam, spam.Note));
        Assert.Null(MailAnswer.Parse("""{"isSpam":false,"isDocument":false,"note":""}""", false, Text, out _).Note);
    }

    [Fact]
    public void Parse_NeverCallsAKnownSenderSpam()
    {
        var answer = MailAnswer.Parse("""{"isSpam":true,"isDocument":true,"note":"Zahlung bis 03.12.2026."}""", true, Text, out _);

        Assert.Equal((false, "Zahlung bis 03.12.2026."), (answer.IsSpam, answer.Note));
    }

    [Theory]
    [InlineData("no json")]
    [InlineData("[]")]
    [InlineData("""{"isSpam":"no","isDocument":true,"note":""}""")]
    [InlineData("""{"isDocument":true,"note":""}""")]
    public void Parse_RefusesAnUnusableAnswer(string content) =>
        Assert.Throws<InvalidDataException>(() => MailAnswer.Parse(content, false, Text, out _));
}
