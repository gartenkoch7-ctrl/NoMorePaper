using Microsoft.Extensions.Logging;

namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// Moves the mails of one run of <see cref="ProcessMailbox"/> out of the inbox and remembers what is needed so
/// that a failed move is only retried by a later run: never a second intake, never a second LLM check.
/// </summary>
/// <remarks>
/// A failed move is no failure of the mail: it stays where it is, a later run tries again, and the run reports
/// it (<see cref="Unmoved"/>).
/// </remarks>
internal sealed class MailMoves(IMailbox mailbox, ISeenItems seen, TimeProvider clock, ILogger logger)
{
    private const InputKind Mail = InputKind.Mail;

    /// <summary>Moves that failed in this run.</summary>
    public int Unmoved { get; private set; }

    /// <returns><c>true</c> when an earlier run dealt with the mail already and only its move was left.</returns>
    public async Task<bool> FinishEarlierAsync(MailHandle handle, CancellationToken cancellationToken)
    {
        var key = handle.ItemKey;
        if (await seen.HasSeenAsync(Mail, null, SeenList.Imported, key, cancellationToken).ConfigureAwait(false))
        {
            await MoveAsync(handle, MailFolder.Processed, cancellationToken).ConfigureAwait(false);
            logger.Processed(0, "already-imported");
            return true;
        }

        if (!await seen.HasSeenAsync(Mail, null, SeenList.SpamMovePending, key, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await MoveSpamAsync(handle, cancellationToken).ConfigureAwait(false);
        logger.Processed(0, "already-spam");
        return true;
    }

    /// <summary>The mail was taken in and is remembered as imported already.</summary>
    public Task ProcessedAsync(MailHandle handle, CancellationToken cancellationToken) =>
        MoveAsync(handle, MailFolder.Processed, cancellationToken);

    /// <summary>
    /// Remembered before the move, so a failed move leaves only the move, not a second LLM check; only once
    /// the mail is in the spam folder does it count as spam (back in the inbox it was moved back on purpose).
    /// </summary>
    public async Task SpamAsync(MailHandle handle, CancellationToken cancellationToken)
    {
        await seen.MarkSeenAsync(Mail, null, SeenList.SpamMovePending, handle.ItemKey, clock.GetUtcNow(), CancellationToken.None)
            .ConfigureAwait(false);
        await MoveSpamAsync(handle, cancellationToken).ConfigureAwait(false);
    }

    /// <returns><c>true</c> when the mail is in the folder for failures now.</returns>
    public Task<bool> PutAsideAsync(MailHandle handle, CancellationToken cancellationToken) =>
        MoveAsync(handle, MailFolder.Failed, cancellationToken);

    private async Task MoveSpamAsync(MailHandle handle, CancellationToken cancellationToken)
    {
        if (await MoveAsync(handle, MailFolder.Spam, cancellationToken).ConfigureAwait(false))
        {
            await seen.MarkSeenAsync(Mail, null, SeenList.Spam, handle.ItemKey, clock.GetUtcNow(), CancellationToken.None).ConfigureAwait(false);
            await seen.ForgetAsync(Mail, null, SeenList.SpamMovePending, handle.ItemKey, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task<bool> MoveAsync(MailHandle handle, MailFolder target, CancellationToken cancellationToken)
    {
        try
        {
            await mailbox.MoveAsync(handle, target, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.MoveFailed(target, exception.GetType().Name);
            Unmoved++;
            return false;
        }
    }
}
