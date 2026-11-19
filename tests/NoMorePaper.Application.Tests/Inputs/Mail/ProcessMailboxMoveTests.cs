using NoMorePaper.Application.Inputs;
using NoMorePaper.Application.Inputs.Mail;
using Xunit;

namespace NoMorePaper.Application.Tests.Inputs.Mail;

/// <summary>Failed moves out of the inbox: retried without a second check, reported, and never starving new mail.</summary>
public sealed class ProcessMailboxMoveTests
{
    private static readonly InputProblem s_stuck = new(InputProblemKind.StuckItems, NeedsAttention: true);

    private readonly MailHarness _mail = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SpamWhoseMoveFailed_IsNotJudgedAgain_OnlyItsMoveIsRetried_AndNotTakenAsMovedBack()
    {
        _mail.Assessor.Answer = new MailAssessment(true, true, null);
        _mail.Mailbox.MoveFailure = new IOException("gone");
        var handle = _mail.Mailbox.Add(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment()));

        Assert.Equal(s_stuck, (await _mail.RunAsync()).Problem);
        Assert.True(_mail.Seen.Has(SeenList.SpamMovePending, handle.ItemKey));
        Assert.False(_mail.Seen.Has(SeenList.Spam, handle.ItemKey));
        Assert.Equal(s_stuck, (await _mail.RunAsync()).Problem);
        _mail.Mailbox.MoveFailure = null;
        var moved = await _mail.RunAsync();

        Assert.Null(moved.Problem);
        Assert.Single(_mail.Assessor.Asked);
        Assert.Equal(1, _mail.Mailbox.Reads);
        Assert.Empty(_mail.Documents.Added);
        Assert.Equal([(handle, MailFolder.Spam)], _mail.Mailbox.Moved);
        Assert.True(_mail.Seen.Has(SeenList.Spam, handle.ItemKey));
        Assert.False(_mail.Seen.Has(SeenList.SpamMovePending, handle.ItemKey));
    }

    [Fact]
    public async Task AFailedMove_IsReported_UntilItSucceeds()
    {
        _mail.Mailbox.MoveFailure = new IOException("connection reset");
        _mail.Mailbox.Add(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment()));

        var first = await _mail.RunAsync();
        var second = await _mail.RunAsync();
        _mail.Mailbox.MoveFailure = null;
        var third = await _mail.RunAsync();

        Assert.Equal((1, s_stuck), (first.Received, first.Problem));
        Assert.Equal((0, s_stuck), (second.Received, second.Problem));
        Assert.Null(third.Problem);
        Assert.Single(_mail.Documents.Added);
        Assert.Equal(2, _mail.Log.EventIds.Count(id => id == 5409));
    }

    [Fact]
    public async Task AMailPutAsideWhoseMoveFails_IsReported()
    {
        _mail.Storage.SaveFailure = new IOException("disk full");
        _mail.Mailbox.Add(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment()));
        await _mail.RunAsync();
        await _mail.RunAsync();
        _mail.Mailbox.MoveFailure = new IOException("gone");

        var third = await _mail.RunAsync();

        Assert.Equal(s_stuck, third.Problem);
        Assert.Contains(5409, _mail.Log.EventIds);
        Assert.Single(_mail.Mailbox.Inbox);
    }

    [Fact]
    public async Task MailsWaitingOnlyForTheirMove_DoNotStarveNewMail()
    {
        _mail.Mailbox.MoveFailure = new IOException("folder gone");
        for (var i = 0; i < ProcessMailbox.MaxMailsPerRun + 5; i++)
        {
            var stuck = _mail.Mailbox.Add(MailHarness.Mail());
            await _mail.Seen.MarkSeenAsync(InputKind.Mail, null, i % 2 == 0 ? SeenList.Imported : SeenList.SpamMovePending, stuck.ItemKey, DateTimeOffset.UnixEpoch, Ct);
        }

        _mail.Mailbox.Add(MailHarness.Mail(SenderCheck.Unknown, MailHarness.PdfAttachment()));

        var result = await _mail.RunAsync();

        Assert.Equal(1, _mail.Mailbox.Reads);
        Assert.Single(_mail.Assessor.Asked);
        Assert.Equal((1, s_stuck), (result.Received, result.Problem));
    }
}
