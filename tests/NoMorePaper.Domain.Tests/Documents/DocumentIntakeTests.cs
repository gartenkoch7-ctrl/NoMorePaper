using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Domain.Tests.Documents;

/// <summary>Source, "from the mail" and the owner check of documents whose owner was unknown (ADR-0024).</summary>
public sealed class DocumentIntakeTests
{
    private static readonly DateTimeOffset s_receivedAt = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_admin = Guid.CreateVersion7();
    private static readonly Guid s_member = Guid.CreateVersion7();
    private static readonly Guid s_editor = Guid.CreateVersion7();

    [Fact]
    public void Receive_RecordsTheSource_UploadByDefault()
    {
        Assert.Equal(DocumentSource.Upload, Document.Receive("a", s_receivedAt, s_admin, isPrivate: false).Source);
        Assert.Equal(DocumentSource.Mail, Document.Receive("a", s_receivedAt, s_admin, isPrivate: false, DocumentSource.Mail).Source);
    }

    [Fact]
    public void AttachMailOrigin_KeepsIt()
    {
        var document = Document.Receive("a", s_receivedAt, s_admin, isPrivate: false, DocumentSource.Mail);
        var origin = new MailOrigin("a@b.example", "Betreff", null);

        document.AttachMailOrigin(origin);

        Assert.Same(origin, document.MailOrigin);
    }

    [Fact]
    public void MarkOwnerCheck_MakesTheDocumentPrivate()
    {
        var document = Unknown();

        Assert.True(document.NeedsOwnerCheck);
        Assert.True(document.IsPrivate);
    }

    [Fact]
    public void ConfirmOwnerCheck_ClearsTheFlag_AndKeepsThePrivacy()
    {
        var document = Unknown();

        document.ConfirmOwnerCheck(s_admin);

        Assert.False(document.NeedsOwnerCheck);
        Assert.True(document.IsPrivate);
    }

    [Fact]
    public void ConfirmOwnerCheck_OnlyByTheOwner_AndOnlyWhenWaiting()
    {
        var document = Unknown();
        Assert.Throws<InvalidOperationException>(() => document.ConfirmOwnerCheck(s_member));

        document.ConfirmOwnerCheck(s_admin);
        Assert.Throws<InvalidOperationException>(() => document.ConfirmOwnerCheck(s_admin));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HandOver_GivesItToTheNewOwner_WithItsPrivacy_AndDropsEditors(bool isPrivate)
    {
        var document = Unknown();
        document.Share(s_admin);
        document.SetEditors(s_admin, [s_editor]);

        document.HandOver(s_admin, s_member, isPrivate);

        Assert.Equal((s_member, isPrivate, false), (document.OwnerId, document.IsPrivate, document.NeedsOwnerCheck));
        Assert.Empty(document.Editors);
    }

    [Fact]
    public void HandOver_DropsFieldsNobodyConfirmed_AndRestoresTheReceivedTitle()
    {
        var document = Unknown();
        var date = new DateOnly(2026, 11, 1);
        document.Classify(new DocumentFields("Stromrechnung", Guid.CreateVersion7(), Guid.CreateVersion7(), date, new Money(10m, "EUR"), "Abschlag.", [Guid.CreateVersion7()]));

        document.HandOver(s_admin, s_member, isPrivate: false);

        Assert.Equal(("Brief 7", RecognitionState.None), (document.Title, document.Recognition));
        Assert.Null(document.CorrespondentId);
        Assert.Null(document.DocumentTypeId);
        Assert.Null(document.Summary);
        Assert.Empty(document.Tags);
    }

    [Fact]
    public void HandOver_KeepsConfirmedFields()
    {
        var document = Unknown();
        var sender = Guid.CreateVersion7();
        document.Edit(s_admin, new DocumentFields("Vertrag", sender, null, null, null, "Kurz.", []));

        document.HandOver(s_admin, s_member, isPrivate: true);

        Assert.Equal(("Vertrag", sender, RecognitionState.Confirmed), (document.Title, document.CorrespondentId, document.Recognition));
    }

    [Fact]
    public void HandOver_DropsHiddenEntries_FromConfirmedFields_AndKeepsTheRest()
    {
        var document = Unknown();
        var (hiddenSender, hiddenType, hiddenTag, sharedTag) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        document.Edit(s_admin, new DocumentFields("Vertrag", hiddenSender, hiddenType, null, null, "Kurz.", [hiddenTag, sharedTag]));

        document.HandOver(s_admin, s_member, isPrivate: false, [hiddenSender, hiddenType, hiddenTag]);

        Assert.Null(document.CorrespondentId);
        Assert.Null(document.DocumentTypeId);
        Assert.Equal([sharedTag], document.Tags.Select(tag => tag.TagId));
        Assert.Equal(("Vertrag", "Kurz.", RecognitionState.Confirmed), (document.Title, document.Summary, document.Recognition));
    }

    [Fact]
    public void HandOver_IsRefused_ForOthers_WithoutCheck_AndToNobodyOrSelf()
    {
        var document = Unknown();
        Assert.Throws<InvalidOperationException>(() => document.HandOver(s_member, s_editor, isPrivate: false));
        Assert.Throws<ArgumentException>(() => document.HandOver(s_admin, Guid.Empty, isPrivate: false));
        Assert.Throws<ArgumentException>(() => document.HandOver(s_admin, s_admin, isPrivate: false));

        var known = Document.Receive("a", s_receivedAt, s_admin, isPrivate: false);
        Assert.Throws<InvalidOperationException>(() => known.HandOver(s_admin, s_member, isPrivate: false));
    }

    [Theory]
    [InlineData(".pdf", "document")]
    [InlineData("Scan.2026.11.pdf", "Scan.2026.11")]
    [InlineData("ohne Endung", "ohne Endung")]
    public void HandOver_TitleFromTheReceivedName(string fileName, string title)
    {
        var document = Unknown(fileName);
        document.Classify(new DocumentFields("Erkannt", null, null, null, null, null, []));

        document.HandOver(s_admin, s_member, isPrivate: false);

        Assert.Equal(title, document.Title);
    }

    private static Document Unknown(string fileName = "Brief 7.pdf")
    {
        var document = Document.Receive("Brief 7", s_receivedAt, s_admin, isPrivate: false, DocumentSource.ScannerFolder);
        document.AttachFile(new DocumentFile(DocumentFileKind.Original, fileName, "application/pdf", 10, new string('a', 64)));
        document.MarkOwnerCheck();
        return document;
    }
}
