using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

public sealed class MailOriginTests
{
    [Fact]
    public void KeepsSenderSubjectAndNote()
    {
        var origin = new MailOrigin(" rechnung@stadtwerke.example ", "Ihre Rechnung", "Zahlung bis 30.11. per Lastschrift.");

        Assert.Equal(
            ("rechnung@stadtwerke.example", "Ihre Rechnung", "Zahlung bis 30.11. per Lastschrift."),
            (origin.SenderAddress, origin.Subject, origin.Note));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNote_IsNone(string? note) =>
        Assert.Null(new MailOrigin("a@b.example", "Betreff", note).Note);

    [Fact]
    public void LongSubjectsAndNotes_AreCut()
    {
        var origin = new MailOrigin("a@b.example", new string('s', 400), new string('n', 700));

        Assert.Equal((MailOrigin.MaxSubjectLength, MailOrigin.MaxNoteLength), (origin.Subject.Length, origin.Note!.Length));
    }

    [Fact]
    public void ControlCharacters_BecomeSpaces()
    {
        var origin = new MailOrigin("a@b.example", "Teil\r\n 2\0", "Frist\tbald");

        Assert.Equal(("Teil   2", "Frist bald"), (origin.Subject, origin.Note));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void ASender_IsRequired(string sender) =>
        Assert.Throws<ArgumentException>(() => new MailOrigin(sender, "Betreff", null));

    [Fact]
    public void ATooLongSender_IsRefused() =>
        Assert.Throws<ArgumentException>(() => new MailOrigin(new string('a', 315) + "@b.example", "Betreff", null));

    [Fact]
    public void AnEmptySubject_IsAllowed() =>
        Assert.Equal(string.Empty, new MailOrigin("a@b.example", string.Empty, null).Subject);
}
