using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Inputs.Mail;

/// <summary>Which parts of a mail become documents, and the text PDF of a mail without them.</summary>
public sealed class MailIntakeTests
{
    private static readonly byte[] s_jpegHead = [0xFF, 0xD8, 0xFF, 0xE0];

    private readonly MailHarness _mail = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true, 40_000, 2480, 3508, "image/jpeg")]
    [InlineData(true, 40_000, null, null, "image/jpeg")]
    [InlineData(false, 40_000, 2480, 3508, null)]
    [InlineData(true, 20_000, 2480, 3508, null)]
    [InlineData(true, 40_000, 600, 120, null)]
    [InlineData(true, 40_000, 1, 1, null)]
    public void AnImage_IsADocument_OnlyAsABigEnoughAttachment(bool isAttachment, int bytes, int? width, int? height, string? expected)
    {
        var content = new byte[bytes];
        s_jpegHead.CopyTo(content, 0);

        Assert.Equal(expected, MailIntake.DocumentType(new MailAttachment("Foto.jpg", "image/jpeg", isAttachment, content, width, height)));
    }

    [Fact]
    public void APdf_IsADocument_EvenInline_AndOtherFilesNever()
    {
        Assert.Equal("application/pdf", MailIntake.DocumentType(MailHarness.PdfAttachment() with { IsAttachment = false }));
        Assert.Null(MailIntake.DocumentType(new MailAttachment("a.docx", "application/vnd.openxmlformats", true, "PK\x03\x04"u8.ToArray(), null, null)));
        Assert.Null(MailIntake.DocumentType(new MailAttachment(string.Empty, "message/rfc822", true, ReadOnlyMemory<byte>.Empty, null, null)));

        // The claimed type does not count, the bytes do.
        Assert.Null(MailIntake.DocumentType(new MailAttachment("fake.pdf", "application/pdf", true, "MZ\x90\x00"u8.ToArray(), null, null)));
    }

    [Fact]
    public async Task EveryDocumentPart_BecomesADocument_TheRestIsLeftOut()
    {
        var mail = MailHarness.Mail(
            SenderCheck.Unknown,
            MailHarness.PdfAttachment("Rechnung.pdf"),
            MailHarness.PdfAttachment("AGB.pdf"),
            new MailAttachment("pixel.gif", "image/gif", false, "GIF89a"u8.ToArray(), 1, 1));

        var received = await _mail.Intake().TakeAsync(mail, null, "Hinweis", keepText: true, Ct);

        Assert.Equal(2, received);
        Assert.Equal(["Rechnung.pdf", "AGB.pdf"], _mail.Storage.Inbox.Values.Select(file => file.FileName));
        Assert.Empty(_mail.Pdf.Pages);
        Assert.Equal([5402], _mail.IntakeLog.EventIds);
    }

    [Fact]
    public async Task WithoutDocumentPart_TheTextBecomesAPdf_NamedByDate_WithTheSubjectInside()
    {
        var received = await _mail.Intake().TakeAsync(MailHarness.Mail(), null, null, keepText: true, Ct);

        Assert.Equal(1, received);
        Assert.Equal("Mail 2026-11-19.pdf", Assert.Single(_mail.Storage.Inbox.Values).FileName);
        var page = Assert.Single(_mail.Pdf.Pages);
        Assert.Equal(MailHarness.Subject, page.Heading);
        Assert.Equal(
            [("Von", MailHarness.Sender), ("Datum", "19.11.2026 08:30"), ("Betreff", MailHarness.Subject)],
            page.Header.Select(field => (field.Label, field.Value)));
        Assert.Equal("Mail 2026-11-19", Assert.Single(_mail.Documents.Added).Title);
    }

    [Fact]
    public async Task WithoutDocumentPart_AndNoDocument_NothingIsTaken()
    {
        Assert.Equal(0, await _mail.Intake().TakeAsync(MailHarness.Mail(), null, null, keepText: false, Ct));

        Assert.Empty(_mail.Documents.Added);
        Assert.Empty(_mail.Pdf.Pages);
    }

    [Fact]
    public async Task AFileTheOwnerHasAlready_CountsAsTaken_NotAsNew()
    {
        _mail.Documents.OwnedDuplicate = Guid.CreateVersion7();

        Assert.Equal(0, await _mail.Intake().TakeAsync(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment()), null, null, true, Ct));
        Assert.Empty(_mail.Documents.Added);
    }

    [Fact]
    public async Task AnOwnerRemovedMeanwhile_MakesTheSenderUnknown()
    {
        _mail.Accounts.KnownAccount = false;

        await _mail.Intake().TakeAsync(MailHarness.Mail(SenderCheck.Passed, MailHarness.PdfAttachment()), Guid.CreateVersion7(), null, true, Ct);

        var document = Assert.Single(_mail.Documents.Added);
        Assert.Equal((_mail.Accounts.AdministratorId, true), (document.OwnerId, document.NeedsOwnerCheck));
    }

    [Fact]
    public async Task WithoutAdministrator_TheMailFails()
    {
        _mail.Accounts.AdministratorId = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _mail.Intake().TakeAsync(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment()), null, null, true, Ct));
    }

    [Fact]
    public async Task AMailWithoutSender_IsKeptAsFromAnUnknownSender()
    {
        await _mail.Intake().TakeAsync(MailHarness.Mail() with { From = string.Empty }, null, null, true, Ct);

        var document = Assert.Single(_mail.Documents.Added);
        Assert.Equal(("unknown", DocumentSource.Mail), (document.MailOrigin?.SenderAddress, document.Source));
    }
}
