namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>
/// The household's mailbox during one run of the mail input (ADR-0024), opened and signed in already.
/// Mails stay in the inbox until <see cref="MoveAsync"/> puts them into one of the <see cref="MailFolder"/>s.
/// </summary>
/// <remarks>
/// Errors of the connection (network, server) are thrown as they are and end the run; only
/// <see cref="MailUnreadableException"/> is a fault of the mail itself.
/// </remarks>
public interface IMailbox
{
    /// <summary>The mails in the inbox, oldest first.</summary>
    /// <param name="max">Most mails returned; a run never takes more.</param>
    Task<IReadOnlyList<MailHandle>> NewMailAsync(int max, CancellationToken cancellationToken);

    /// <returns>The mail, or <c>null</c> when it is no longer in the inbox (moved or deleted meanwhile).</returns>
    /// <exception cref="MailUnreadableException">The mail cannot be read as MIME.</exception>
    Task<MailContent?> ReadAsync(MailHandle mail, CancellationToken cancellationToken);

    /// <summary>Moves the mail out of the inbox into <paramref name="folder"/>.</summary>
    Task MoveAsync(MailHandle mail, MailFolder folder, CancellationToken cancellationToken);
}
