namespace NoMorePaper.Application.Inputs.Mail;

/// <summary>Tries a mailbox set-up: connect, sign in, sign off; nothing is read or moved.</summary>
public interface IMailboxProbe
{
    /// <param name="password">In plain text, only for this call; never stored or logged.</param>
    Task<MailboxProbeResult> ProbeAsync(MailboxSettings settings, string password, CancellationToken cancellationToken);
}
