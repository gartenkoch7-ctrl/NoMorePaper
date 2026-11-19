using Microsoft.Extensions.Logging;
using NoMorePaper.Application.Accounts;

namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// One run over the household's inbox (ADR-0024): sender → account (only if the own mail server confirmed
/// it), LLM check, spam into its folder, everything else taken in and moved to "processed".
/// </summary>
/// <remarks>
/// A mail is never lost and never taken twice: it leaves the inbox only after its documents are committed,
/// and it is remembered as imported before it is moved. Only faults of the mail itself (unreadable MIME,
/// unusable LLM answer, intake or PDF failing) count against it; after <see cref="MaxFailures"/> it is put
/// aside. When the LLM does not answer, the run ends and the mail stays untouched.
/// </remarks>
public sealed class ProcessMailbox(
    ISeenItems seen,
    IAccountPreferencesStore preferences,
    IMailAssessor assessor,
    MailIntake intake,
    TimeProvider clock,
    ILogger<ProcessMailbox> logger)
{
    /// <summary>Each mail of an unknown sender costs an LLM request that competes with recognition.</summary>
    public const int MaxMailsPerRun = 20;

    /// <summary>Failures after which a mail goes to <see cref="MailFolder.Failed"/>.</summary>
    public const int MaxFailures = 3;

    /// <summary>No mail is started after this time; the run interval is 5 minutes as well.</summary>
    public static readonly TimeSpan RunBudget = TimeSpan.FromMinutes(5);

    private const InputKind Mail = InputKind.Mail;

    public async Task<InputRunResult> RunAsync(IMailbox mailbox, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mailbox);
        var started = clock.GetTimestamp();
        var handles = await mailbox.NewMailAsync(MaxMailsPerRun, cancellationToken).ConfigureAwait(false);
        var tally = new Tally();
        InputProblem? problem = null;
        foreach (var handle in handles)
        {
            if (clock.GetElapsedTime(started) >= RunBudget)
            {
                logger.BudgetReached(tally.Mails);
                break;
            }

            if (!await ProcessAsync(mailbox, handle, tally, cancellationToken).ConfigureAwait(false))
            {
                logger.LlmUnavailable(handles.Count - tally.Mails);
                problem = new InputProblem(InputProblemKind.LlmUnavailable);
                break;
            }

            tally.Mails++;
        }

        var elapsedMs = (long)clock.GetElapsedTime(started).TotalMilliseconds;
        logger.Run(tally.Mails, tally.Received, tally.Skipped, tally.Stuck, elapsedMs);

        // Mails put aside need somebody to look into the folder; that matters more than a busy LLM.
        if (tally.Stuck > 0)
        {
            problem = new InputProblem(InputProblemKind.StuckItems, NeedsAttention: true);
        }

        return new InputRunResult(tally.Received, tally.Skipped, problem);
    }

    /// <returns><c>false</c> when the LLM did not answer and the run must end.</returns>
    private async Task<bool> ProcessAsync(IMailbox mailbox, MailHandle handle, Tally tally, CancellationToken cancellationToken)
    {
        var key = handle.ItemKey;
        if (await seen.HasSeenAsync(Mail, null, SeenList.Imported, key, cancellationToken).ConfigureAwait(false))
        {
            // Taken in by an earlier run whose move failed: only the move is left.
            await MoveAsync(mailbox, handle, MailFolder.Processed, cancellationToken).ConfigureAwait(false);
            logger.Processed(0, "already-imported");
            return true;
        }

        MailContent? mail;
        try
        {
            mail = await mailbox.ReadAsync(handle, cancellationToken).ConfigureAwait(false);
        }
        catch (MailUnreadableException exception)
        {
            await FailedAsync(mailbox, handle, exception, tally, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (mail is null)
        {
            return true;
        }

        var backFromSpam = await seen.HasSeenAsync(Mail, null, SeenList.Spam, key, cancellationToken).ConfigureAwait(false);
        var account = mail.From.Length == 0 ? null : await preferences.FindOwnerOfSenderAsync(mail.From, cancellationToken).ConfigureAwait(false);
        var routing = MailRouting.Decide(account, mail.FromCheck, backFromSpam);
        var types = mail.Attachments.Select(MailIntake.DocumentType).OfType<string>().ToList();

        MailAssessment assessment;
        try
        {
            assessment = await assessor.AssessAsync(MailText.From(mail, types), knownSender: !routing.NeedsSpamCheck, cancellationToken).ConfigureAwait(false);
        }
        catch (LlmUnavailableException)
        {
            return false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.AssessmentFailed(exception.GetType().Name);
            await FailedAsync(mailbox, handle, exception, tally, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (routing.NeedsSpamCheck && assessment.IsSpam)
        {
            await SpamAsync(mailbox, handle, tally, cancellationToken).ConfigureAwait(false);
            return true;
        }

        MailLog.Assessed.Add(1, new KeyValuePair<string, object?>("nomorepaper.mail.verdict", assessment.IsDocument ? "document" : "no-document"));
        int received;
        try
        {
            // Moved back from the spam folder on purpose: worth keeping even without attachment.
            received = await intake.TakeAsync(mail, routing.OwnerId, assessment.Note, assessment.IsDocument || backFromSpam, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await FailedAsync(mailbox, handle, exception, tally, cancellationToken).ConfigureAwait(false);
            return true;
        }

        // The documents are committed: remembering must not be cancelled now, or the next run takes the mail
        // again. Remembered before the move, so a failed move leaves only the move for later.
        await seen.MarkSeenAsync(Mail, null, SeenList.Imported, key, clock.GetUtcNow(), CancellationToken.None).ConfigureAwait(false);
        await seen.ForgetAsync(Mail, null, SeenList.Failed, key, CancellationToken.None).ConfigureAwait(false);
        tally.Received += received;
        tally.Skipped += received == 0 ? 1 : 0;
        logger.Processed(received, received == 0 ? "no-document" : "document");
        await MoveAsync(mailbox, handle, MailFolder.Processed, cancellationToken).ConfigureAwait(false);
        return true;
    }

    // Moved first, remembered after: a mail that is still in the inbox because its move failed must be
    // judged again, not taken as one moved back from the spam folder.
    private async Task SpamAsync(IMailbox mailbox, MailHandle handle, Tally tally, CancellationToken cancellationToken)
    {
        MailLog.Assessed.Add(1, new KeyValuePair<string, object?>("nomorepaper.mail.verdict", "spam"));
        tally.Skipped++;
        logger.Processed(0, "spam");
        if (await MoveAsync(mailbox, handle, MailFolder.Spam, cancellationToken).ConfigureAwait(false))
        {
            await seen.MarkSeenAsync(Mail, null, SeenList.Spam, handle.ItemKey, clock.GetUtcNow(), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task FailedAsync(IMailbox mailbox, MailHandle handle, Exception exception, Tally tally, CancellationToken cancellationToken)
    {
        var failures = await seen.IncrementFailuresAsync(Mail, null, handle.ItemKey, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        logger.Failed(exception.GetType().Name, failures, MaxFailures);
        if (failures < MaxFailures)
        {
            return;
        }

        // Only a moved mail starts counting anew; one still in the inbox goes on the next run.
        if (await MoveAsync(mailbox, handle, MailFolder.Failed, cancellationToken).ConfigureAwait(false))
        {
            await seen.ForgetAsync(Mail, null, SeenList.Failed, handle.ItemKey, CancellationToken.None).ConfigureAwait(false);
            logger.PutAside(failures);
            tally.Stuck++;
        }
    }

    // A failed move is no failure of the mail: it stays where it is and a later run tries again.
    private async Task<bool> MoveAsync(IMailbox mailbox, MailHandle handle, MailFolder target, CancellationToken cancellationToken)
    {
        try
        {
            await mailbox.MoveAsync(handle, target, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.MoveFailed(target, exception.GetType().Name);
            return false;
        }
    }

    private sealed class Tally
    {
        public int Mails { get; set; }

        public int Received { get; set; }

        public int Skipped { get; set; }

        public int Stuck { get; set; }
    }
}
