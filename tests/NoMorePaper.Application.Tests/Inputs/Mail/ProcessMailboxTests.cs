using NoMorePaper.Application.Accounts;
using NoMorePaper.Application.Inputs;
using NoMorePaper.Application.Inputs.Mail;
using NoMorePaper.Domain.Documents;
using Xunit;

namespace NoMorePaper.Application.Tests.Inputs.Mail;

/// <summary>A run over the inbox: routing, spam, intake and moving, in that order (ADR-0024).</summary>
public sealed class ProcessMailboxTests
{
    private readonly MailHarness _mail = new();

    [Fact]
    public async Task AnAttachmentOfAnUnknownSender_GoesToTheAdministrator_WithItsOrigin_ThenTheMailIsProcessed()
    {
        var handle = _mail.Mailbox.Add(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment()));

        var result = await _mail.RunAsync();

        var document = Assert.Single(_mail.Documents.Added);
        Assert.Equal((_mail.Accounts.AdministratorId, true, DocumentSource.Mail), (document.OwnerId, document.NeedsOwnerCheck, document.Source));
        Assert.Equal(new MailOrigin(MailHarness.Sender, MailHarness.Subject, "Zahlbar bis 30.11."), document.MailOrigin);
        Assert.Equal([(handle, MailFolder.Processed)], _mail.Mailbox.Moved);
        Assert.True(_mail.Seen.Has(SeenList.Imported, handle.ItemKey));
        Assert.Equal(new InputRunResult(1, 0), result);
        var asked = Assert.Single(_mail.Assessor.Asked);
        Assert.Equal(("stadtwerke.example", false), (asked.Mail.SenderDomain, asked.KnownSender));
        Assert.Equal(["application/pdf"], asked.Mail.AttachmentTypes);
    }

    [Fact]
    public async Task Spam_GoesToTheSpamFolder_AndIsRememberedAfterTheMove()
    {
        _mail.Assessor.Answer = new MailAssessment(true, false, null);
        var handle = _mail.Mailbox.Add(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment()));

        var result = await _mail.RunAsync();

        Assert.Empty(_mail.Documents.Added);
        Assert.Equal([(handle, MailFolder.Spam)], _mail.Mailbox.Moved);
        Assert.True(_mail.Seen.Has(SeenList.Spam, handle.ItemKey));
        Assert.Equal(new InputRunResult(0, 1), result);
    }

    [Fact]
    public async Task SpamWhoseMoveFailed_IsJudgedAgain_NotTakenAsMovedBack()
    {
        _mail.Assessor.Answer = new MailAssessment(true, true, null);
        _mail.Mailbox.MoveFailure = new IOException("gone");
        var handle = _mail.Mailbox.Add(MailHarness.Mail());

        await _mail.RunAsync();
        _mail.Mailbox.MoveFailure = null;
        await _mail.RunAsync();

        Assert.Empty(_mail.Documents.Added);
        Assert.Equal([(handle, MailFolder.Spam)], _mail.Mailbox.Moved);
        Assert.Equal([false, false], _mail.Assessor.Asked.Select(asked => asked.KnownSender));
    }

    [Fact]
    public async Task AMailMovedBackFromSpam_IsTakenWithoutSpamCheck_EvenWithoutAttachment()
    {
        _mail.Assessor.Answer = new MailAssessment(true, false, null);
        var handle = _mail.Mailbox.Add(MailHarness.Mail(), "id:back@example");
        await _mail.Seen.MarkSeenAsync(InputKind.Mail, null, SeenList.Spam, handle.ItemKey, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        await _mail.RunAsync();

        Assert.True(Assert.Single(_mail.Assessor.Asked).KnownSender);
        Assert.Single(_mail.Documents.Added);
        Assert.Equal(MailHarness.Subject, Assert.Single(_mail.Pdf.Pages).Heading);
        Assert.Equal([(handle, MailFolder.Processed)], _mail.Mailbox.Moved);
    }

    [Fact]
    public async Task AConfirmedSenderOfAnAccount_OwnsTheDocument_AndIsNeverSpam()
    {
        var anna = Guid.CreateVersion7();
        _mail.Preferences.Saved[anna] = new AccountPreferences(false, [MailHarness.Sender]);
        _mail.Assessor.Answer = new MailAssessment(true, false, null);
        _mail.Mailbox.Add(MailHarness.Mail(SenderCheck.Passed, MailHarness.PdfAttachment()));

        await _mail.RunAsync();

        var document = Assert.Single(_mail.Documents.Added);
        Assert.Equal((anna, false), (document.OwnerId, document.NeedsOwnerCheck));
        Assert.True(Assert.Single(_mail.Assessor.Asked).KnownSender);
    }

    [Theory]
    [InlineData(SenderCheck.Unknown)]
    [InlineData(SenderCheck.Failed)]
    public async Task AnAccountsAddressWithoutConfirmation_IsAnUnknownSender_AndGetsTheSpamCheck(SenderCheck check)
    {
        _mail.Preferences.Saved[Guid.CreateVersion7()] = new AccountPreferences(false, [MailHarness.Sender]);
        _mail.Assessor.Answer = new MailAssessment(true, false, null);
        var handle = _mail.Mailbox.Add(MailHarness.Mail(check, MailHarness.PdfAttachment()));

        await _mail.RunAsync();

        Assert.False(Assert.Single(_mail.Assessor.Asked).KnownSender);
        Assert.Empty(_mail.Documents.Added);
        Assert.Equal([(handle, MailFolder.Spam)], _mail.Mailbox.Moved);
    }

    [Fact]
    public async Task WithoutTheLlm_TheRunStops_AndTheMailStaysUntouched_ForAnyNumberOfRuns()
    {
        _mail.Assessor.Failure = new LlmUnavailableException();
        _mail.Mailbox.Add(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment()));
        _mail.Mailbox.Add(MailHarness.Mail());

        for (var run = 0; run < 3; run++)
        {
            Assert.Equal(InputProblemKind.LlmUnavailable, (await _mail.RunAsync()).Problem?.Kind);
        }

        Assert.Equal(2, _mail.Mailbox.Inbox.Count);
        Assert.Empty(_mail.Mailbox.Moved);
        Assert.Empty(_mail.Seen.Items);
        Assert.Empty(_mail.Documents.Added);
        Assert.Equal(3, _mail.Assessor.Asked.Count);
    }

    [Fact]
    public async Task AFailedMove_LeavesOnlyTheMoveForTheNextRun_NoSecondDocument()
    {
        _mail.Mailbox.MoveFailure = new IOException("connection reset");
        var handle = _mail.Mailbox.Add(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment()));

        Assert.Equal(1, (await _mail.RunAsync()).Received);
        _mail.Mailbox.MoveFailure = null;
        var second = await _mail.RunAsync();

        Assert.Single(_mail.Documents.Added);
        Assert.Single(_mail.Assessor.Asked);
        Assert.Equal([(handle, MailFolder.Processed)], _mail.Mailbox.Moved);
        Assert.Equal(new InputRunResult(0, 0), second);
        Assert.Contains(5403, _mail.Log.EventIds);
    }

    [Fact]
    public async Task AMailWhoseIntakeFailsThreeTimes_IsPutAside_AndReported()
    {
        _mail.Storage.SaveFailure = new IOException("disk full");
        var handle = _mail.Mailbox.Add(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment()));

        Assert.Null((await _mail.RunAsync()).Problem);
        Assert.Null((await _mail.RunAsync()).Problem);
        Assert.Equal(2, _mail.Seen.Items[(SeenList.Failed, handle.ItemKey)]);
        var third = await _mail.RunAsync();

        Assert.Equal(new InputProblem(InputProblemKind.StuckItems, NeedsAttention: true), third.Problem);
        Assert.Equal([(handle, MailFolder.Failed)], _mail.Mailbox.Moved);
        Assert.Empty(_mail.Seen.Items);
        Assert.Contains(5404, _mail.Log.EventIds);
    }

    [Fact]
    public async Task AnUnreadableMail_CountsAgainstItself_AndAnUnusableAnswerToo()
    {
        var unreadable = _mail.Mailbox.Add(null);
        _mail.Mailbox.Add(MailHarness.Mail(), "id:odd@example");
        _mail.Assessor.Failure = new InvalidDataException("no JSON");

        await _mail.RunAsync();

        Assert.Equal(1, _mail.Seen.Items[(SeenList.Failed, unreadable.ItemKey)]);
        Assert.Equal(1, _mail.Seen.Items[(SeenList.Failed, "id:odd@example")]);
        Assert.Contains(5401, _mail.Log.EventIds);
        Assert.Empty(_mail.Mailbox.Moved);
    }

    [Fact]
    public async Task AMailThatIsNoDocument_IsProcessedWithoutDocument()
    {
        _mail.Assessor.Answer = new MailAssessment(false, false, null);
        var handle = _mail.Mailbox.Add(MailHarness.Mail());

        var result = await _mail.RunAsync();

        Assert.Empty(_mail.Documents.Added);
        Assert.Empty(_mail.Pdf.Pages);
        Assert.Equal([(handle, MailFolder.Processed)], _mail.Mailbox.Moved);
        Assert.Equal(new InputRunResult(0, 1), result);
    }

    [Fact]
    public async Task ARun_TakesAtMostTwentyMails_AndStartsNoneAfterItsBudget()
    {
        for (var i = 0; i < 25; i++)
        {
            _mail.Mailbox.Add(MailHarness.Mail());
        }

        await _mail.RunAsync();
        Assert.Equal(20, _mail.Mailbox.Reads);

        _mail.Clock.Step = TimeSpan.FromMinutes(2);
        await _mail.RunAsync();
        Assert.Equal(22, _mail.Mailbox.Reads);
        Assert.Contains(5407, _mail.Log.EventIds);
    }

    [Fact]
    public async Task TheLogs_NameNoAddressSubjectOrText()
    {
        _mail.Mailbox.Add(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment(), new MailAttachment("Logo.png", "image/png; name=Logo.png", false, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 1, 1)));
        _mail.Mailbox.Add(null);

        await _mail.RunAsync();

        Assert.NotEmpty(_mail.AllMessages);
        Assert.DoesNotContain(_mail.AllMessages, message =>
            message.Contains("stadtwerke", StringComparison.OrdinalIgnoreCase) || message.Contains("Rechnung", StringComparison.Ordinal)
            || message.Contains("Logo", StringComparison.Ordinal) || message.Contains("30.11", StringComparison.Ordinal));
        Assert.Contains(_mail.IntakeLog.Messages, message => message.Contains("image/png", StringComparison.Ordinal));
    }
}
